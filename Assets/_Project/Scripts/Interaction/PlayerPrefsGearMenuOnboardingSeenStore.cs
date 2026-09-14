using System;
using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// 「봤음」의 실제 설정 저장소(<c>PlayerPrefs</c>) 구현 — 출하본의 유일한 기본값.
    ///
    /// <para><b>왜 세이브 파일(<c>CharacterSaveStore</c>)이 아닌가</b> (2026-09-01 결정, 위젯에서 옮겨 온 근거):
    /// 필드 하나를 더하려면 스키마 버전을 올리고 마이그레이션을 붙여야 한다. 저장되는 것은 부울 하나이고
    /// 유실돼도 최악이 「안내가 한 번 더 뜬다」다(사용자 데이터가 아니다). 위험 대비 이득이 맞지 않는다.</para>
    ///
    /// <para><b>★ 하위 호환 — 키 이름과 값 형식은 한 글자도 바꾸지 않는다.</b> 2026-09-01부터 출하된 빌드가
    /// 사용자 기계에 이미 이 키로 정수 <see cref="SeenValue"/>를 남겼다. macOS는 앱 번들 ID 도메인의 plist,
    /// Windows는 <c>HKCU\Software\Vibelab\StickMate</c>의 값 — Windows 값 이름 끝의 <c>_h&lt;해시&gt;</c> 꼬리는
    /// Unity가 <b>키 이름에서</b> 만들므로 이름을 바꾸면 꼬리도 바뀐다. 바꾸면 ① 이미 본 사용자에게 안내가 다시
    /// 뜨고 ② 촬영 절차(docs/marketing/CAPTURE_PROTOCOL.md)가 키를 못 찾는다.
    /// 고정값은 이 상수가 아니라 테스트 골든이 따로 들고 있다 — 둘이 갈라지면
    /// <c>GearMenuOnboardingSeenStoreTests</c>가 빨개진다.</para>
    ///
    /// <para><b>계수(<see cref="ReadCount"/>·<see cref="WriteCount"/>)는 테스트 관측용이다.</b> 설정 저장소에
    /// 닿는 길은 <see cref="IsSeen"/>·<see cref="MarkSeen"/> 둘뿐이고 계수는 그 두 함수의 <b>첫 줄</b>, 어떤 분기·반환·
    /// 싱크 호출보다 먼저 오른다. PlayMode 스위트는 끝에 이 계수의 증가분을 0으로 단언해 「주입을 빠뜨린 픽스처」를
    /// 잡는다. 읽기도 세는 이유: 이미 1이 적힌 개발자 기계에서는 주입이 빠져도 <b>쓰기가 일어나지 않는다</b> —
    /// 쓰기만 세면 그 기계에서 누락이 초록으로 숨는다. 같은 이유로 계수가 「이미 봤음이면 먼저 반환」 뒤로 밀리면
    /// 읽기 계수도 숨으므로(verify-change B7) 자기 검증은 <b>안 봤음·봤음 두 경로를 모두</b> 태운다.</para>
    ///
    /// <para><b>싱크를 받는 생성자가 공개인 이유</b>: 계수가 실제 쓰기 경로에 붙어 있음을 <b>실제 저장소를 건드리지
    /// 않고</b> 증명하기 위해서다. 이 클래스의 코드를 그대로 태우고 맨 끝의 한 줄만 기록용으로 바꾼다.</para>
    ///
    /// <para><b>출하본 싱크가 <c>private</c> 중첩인 이유</b> (verify-change B1): <c>internal</c>이면 런타임 어셈블리
    /// 어느 파일에서든 싱크 인스턴스로 설정 저장소를 직접 부를 수 있다. 그 호출은 <c>PlayerPrefs</c> 글자가 없어
    /// 텍스트 감사가 못 보고, 계수를 거치지 않아 PlayMode 0 단언도 못 본다. 이 클래스 밖에서는 <b>컴파일이 막히게</b> 둔다.</para>
    /// </summary>
    public sealed class PlayerPrefsGearMenuOnboardingSeenStore : IGearMenuOnboardingSeenStore
    {
        /// <summary>설정 저장소 키. ★ 프로덕션에서 이 문자열이 정의되는 곳은 여기 하나다.</summary>
        public const string Key = "StickMate.GearMenu.OnboardingSeen.v1";

        /// <summary>「봤음」으로 쓰는 값. 판정은 <b>정확히 이 값</b>일 때만 참이다(옛 코드의 <c>== 1</c> 그대로).</summary>
        public const int SeenValue = 1;

        /// <summary>키가 없을 때 읽히는 값.</summary>
        public const int UnsetValue = 0;

        /// <summary>이 구현을 거친 읽기 횟수(프로세스 누적). 테스트 관측용.</summary>
        public static int ReadCount { get; private set; }

        /// <summary>이 구현을 거친 쓰기 횟수(프로세스 누적). 테스트 관측용.</summary>
        public static int WriteCount { get; private set; }

        /// <summary>정수 설정값 한 칸을 읽고 쓰는 맨 끝 한 줄. 출하본은 항상 이 클래스 안의 <c>private</c> 싱크다.</summary>
        public interface IIntPreferenceSink
        {
            int GetInt(string key, int defaultValue);
            void SetInt(string key, int value);
            void Save();
        }

        private readonly IIntPreferenceSink _sink;

        /// <summary>출하본 생성자 — 실제 <c>PlayerPrefs</c>에 쓴다.</summary>
        public PlayerPrefsGearMenuOnboardingSeenStore() : this(UnityPlayerPrefsSink.Instance) { }

        /// <summary>계수 자기 검증 전용 — 이 클래스의 경로를 그대로 태우되 맨 끝 한 줄만 바꾼다.</summary>
        public PlayerPrefsGearMenuOnboardingSeenStore(IIntPreferenceSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool IsSeen
        {
            get
            {
                // 계수는 어떤 분기보다 먼저 — 「봤음이면 먼저 반환」 뒤로 밀리면 1이 적힌 기계에서 누락이 숨는다.
                ReadCount++;
                return _sink.GetInt(Key, UnsetValue) == SeenValue;
            }
        }

        public void MarkSeen()
        {
            WriteCount++;
            _sink.SetInt(Key, SeenValue);
            // 뜨는 순간 디스크까지 내린다 — 촬영 절차가 테이크 직후 이 값을 읽는다.
            _sink.Save();
        }

        /// <summary>프로덕션에서 <c>PlayerPrefs</c>를 부르는 유일한 자리. 이 클래스 밖에서는 보이지 않는다.</summary>
        private sealed class UnityPlayerPrefsSink : IIntPreferenceSink
        {
            public static readonly UnityPlayerPrefsSink Instance = new UnityPlayerPrefsSink();

            private UnityPlayerPrefsSink() { }

            public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);

            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

            public void Save() => PlayerPrefs.Save();
        }
    }
}
