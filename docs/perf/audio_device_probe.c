/* 오디오 출력 장치를 "열어만 두는" 비용을 재는 프로브.
 * Unity/FMOD가 소리 0개인데도 하던 일과 같은 구조:
 *   - 기본 출력 장치를 열고
 *   - 렌더 콜백에서 무음을 채운다(kAudioUnitRenderAction_OutputIsSilence 플래그는 일부러 세우지 않는다.
 *     FMOD도 세우지 않고 실제로 믹싱한다 — 플래그를 세우면 CoreAudio가 최적화해 버려 비교가 무의미해진다)
 * argv[1] = 버퍼 프레임 수 (예: 512, 4096)
 * argv[2] = 실행 초 (0이면 무한)
 * 표준출력: 1초마다 "sec,callbacks,frames" — 콜백 호출 횟수를 직접 센다(간접 추정 아님)
 */
#include <AudioToolbox/AudioToolbox.h>
#include <AudioUnit/AudioUnit.h>
#include <CoreAudio/CoreAudio.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <stdatomic.h>

static _Atomic unsigned long g_cb = 0;
static _Atomic unsigned long g_frames = 0;

static OSStatus RenderCB(void *inRefCon,
                         AudioUnitRenderActionFlags *ioActionFlags,
                         const AudioTimeStamp *inTimeStamp,
                         UInt32 inBusNumber,
                         UInt32 inNumberFrames,
                         AudioBufferList *ioData)
{
    (void)inRefCon; (void)inTimeStamp; (void)inBusNumber; (void)ioActionFlags;
    for (UInt32 i = 0; i < ioData->mNumberBuffers; i++)
        memset(ioData->mBuffers[i].mData, 0, ioData->mBuffers[i].mDataByteSize);
    atomic_fetch_add(&g_cb, 1);
    atomic_fetch_add(&g_frames, inNumberFrames);
    return noErr;
}

int main(int argc, char **argv)
{
    UInt32 wantFrames = (argc > 1) ? (UInt32)atoi(argv[1]) : 512;
    int seconds = (argc > 2) ? atoi(argv[2]) : 0;

    AudioComponentDescription desc = {0};
    desc.componentType = kAudioUnitType_Output;
    desc.componentSubType = kAudioUnitSubType_HALOutput;   /* AUHAL — 장치를 명시적으로 연다 */
    desc.componentManufacturer = kAudioUnitManufacturer_Apple;

    AudioComponent comp = AudioComponentFindNext(NULL, &desc);
    if (!comp) { fprintf(stderr, "no component\n"); return 1; }

    AudioUnit au;
    OSStatus st = AudioComponentInstanceNew(comp, &au);
    if (st) { fprintf(stderr, "new failed %d\n", (int)st); return 1; }

    /* 기본 출력 장치를 붙인다 */
    AudioObjectPropertyAddress a = { kAudioHardwarePropertyDefaultOutputDevice,
                                     kAudioObjectPropertyScopeGlobal,
                                     kAudioObjectPropertyElementMain };
    AudioDeviceID dev = 0; UInt32 sz = sizeof(dev);
    AudioObjectGetPropertyData(kAudioObjectSystemObject, &a, 0, NULL, &sz, &dev);
    AudioUnitSetProperty(au, kAudioOutputUnitProperty_CurrentDevice,
                         kAudioUnitScope_Global, 0, &dev, sizeof(dev));

    /* 버퍼 프레임 수 — FMOD 기본 DSP 버퍼 512와 맞춘다 */
    AudioUnitSetProperty(au, kAudioDevicePropertyBufferFrameSize,
                         kAudioUnitScope_Global, 0, &wantFrames, sizeof(wantFrames));

    AudioStreamBasicDescription fmt = {0};
    sz = sizeof(fmt);
    AudioUnitGetProperty(au, kAudioUnitProperty_StreamFormat,
                         kAudioUnitScope_Input, 0, &fmt, &sz);

    AURenderCallbackStruct cb = { RenderCB, NULL };
    AudioUnitSetProperty(au, kAudioUnitProperty_SetRenderCallback,
                         kAudioUnitScope_Input, 0, &cb, sizeof(cb));

    st = AudioUnitInitialize(au);
    if (st) { fprintf(stderr, "init failed %d\n", (int)st); return 1; }

    UInt32 actual = 0; sz = sizeof(actual);
    AudioUnitGetProperty(au, kAudioDevicePropertyBufferFrameSize,
                         kAudioUnitScope_Global, 0, &actual, &sz);

    st = AudioOutputUnitStart(au);
    if (st) { fprintf(stderr, "start failed %d\n", (int)st); return 1; }

    fprintf(stderr, "[probe] pid=%d dev=%u askedFrames=%u actualFrames=%u sr=%.0f ch=%u\n",
            (int)getpid(), (unsigned)dev, (unsigned)wantFrames, (unsigned)actual,
            fmt.mSampleRate, (unsigned)fmt.mChannelsPerFrame);
    fflush(stderr);

    unsigned long prev = 0;
    for (int t = 1; seconds == 0 || t <= seconds; t++) {
        sleep(1);
        unsigned long now = atomic_load(&g_cb);
        printf("%d,%lu,%lu\n", t, now - prev, atomic_load(&g_frames));
        fflush(stdout);
        prev = now;
    }
    AudioOutputUnitStop(au);
    AudioUnitUninitialize(au);
    AudioComponentInstanceDispose(au);
    return 0;
}
