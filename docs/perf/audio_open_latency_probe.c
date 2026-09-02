/* "필요할 때만 연다"의 대가를 잰다: 장치 열기 → 첫 콜백까지의 지연.
 * 5회 반복(열고 3초 유지 후 완전히 닫음, 5초 쉼).
 * 매 회차마다 (a) 인스턴스 생성+초기화 시간 (b) Start→첫 콜백 지연 (c) 장치 IsRunning 상태를 찍는다. */
#include <AudioToolbox/AudioToolbox.h>
#include <AudioUnit/AudioUnit.h>
#include <CoreAudio/CoreAudio.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <stdatomic.h>
#include <mach/mach_time.h>

static _Atomic unsigned long g_cb;
static _Atomic uint64_t g_first;
static mach_timebase_info_data_t tb;
static double ms(uint64_t d){ return (double)d * tb.numer / tb.denom / 1e6; }

static OSStatus CB(void *r, AudioUnitRenderActionFlags *f, const AudioTimeStamp *ts,
                   UInt32 b, UInt32 n, AudioBufferList *io){
  (void)r;(void)f;(void)ts;(void)b;(void)n;
  for (UInt32 i=0;i<io->mNumberBuffers;i++) memset(io->mBuffers[i].mData,0,io->mBuffers[i].mDataByteSize);
  uint64_t z=0; atomic_compare_exchange_strong(&g_first,&z,mach_absolute_time());
  atomic_fetch_add(&g_cb,1); return noErr;
}

static AudioDeviceID defdev(void){
  AudioObjectPropertyAddress a={kAudioHardwarePropertyDefaultOutputDevice,kAudioObjectPropertyScopeGlobal,kAudioObjectPropertyElementMain};
  AudioDeviceID d=0; UInt32 s=sizeof(d);
  AudioObjectGetPropertyData(kAudioObjectSystemObject,&a,0,NULL,&s,&d); return d;
}
static UInt32 devrunning(AudioDeviceID d){
  AudioObjectPropertyAddress a={kAudioDevicePropertyDeviceIsRunning,kAudioObjectPropertyScopeGlobal,kAudioObjectPropertyElementMain};
  UInt32 v=0,s=sizeof(v); AudioObjectGetPropertyData(d,&a,0,NULL,&s,&v); return v;
}

int main(void){
  mach_timebase_info(&tb);
  AudioDeviceID dev=defdev();
  printf("dev=%u  IsRunning(초기,아무도 안 씀)=%u\n",(unsigned)dev,devrunning(dev));
  for(int k=1;k<=5;k++){
    atomic_store(&g_cb,0); atomic_store(&g_first,0);
    uint64_t t0=mach_absolute_time();
    AudioComponentDescription d={0}; d.componentType=kAudioUnitType_Output;
    d.componentSubType=kAudioUnitSubType_HALOutput; d.componentManufacturer=kAudioUnitManufacturer_Apple;
    AudioComponent c=AudioComponentFindNext(NULL,&d); AudioUnit au;
    AudioComponentInstanceNew(c,&au);
    AudioUnitSetProperty(au,kAudioOutputUnitProperty_CurrentDevice,kAudioUnitScope_Global,0,&dev,sizeof(dev));
    UInt32 fr=512; AudioUnitSetProperty(au,kAudioDevicePropertyBufferFrameSize,kAudioUnitScope_Global,0,&fr,sizeof(fr));
    AURenderCallbackStruct cb={CB,NULL};
    AudioUnitSetProperty(au,kAudioUnitProperty_SetRenderCallback,kAudioUnitScope_Input,0,&cb,sizeof(cb));
    AudioUnitInitialize(au);
    uint64_t t1=mach_absolute_time();
    AudioOutputUnitStart(au);
    uint64_t t2=mach_absolute_time();
    /* 첫 콜백 대기 */
    uint64_t first=0; int spins=0;
    while(!(first=atomic_load(&g_first)) && spins++ < 200000) usleep(50);
    uint64_t t3=mach_absolute_time();
    UInt32 run=devrunning(dev);
    sleep(3);
    unsigned long n=atomic_load(&g_cb);
    AudioOutputUnitStop(au); AudioUnitUninitialize(au); AudioComponentInstanceDispose(au);
    usleep(300000);
    UInt32 runAfter=devrunning(dev);
    printf("#%d  생성+Initialize=%.1fms  Start호출=%.1fms  Start->첫콜백=%.1fms  3초콜백수=%lu  IsRunning(재생중)=%u  IsRunning(닫은뒤0.3s)=%u\n",
      k, ms(t1-t0), ms(t2-t1), ms(first? first-t2 : t3-t2), n, run, runAfter);
    fflush(stdout);
    sleep(5);
  }
  printf("IsRunning(전부 끝난 뒤)=%u\n",devrunning(dev));
  return 0;
}
