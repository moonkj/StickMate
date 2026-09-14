namespace StickMate.Interaction
{
    /// <summary>
    /// 프로세스 메모리에만 있는 「봤음」 저장소. 테스트 스위트가 실제 설정 저장소 대신 넣는다.
    ///
    /// <para>계수가 인스턴스마다인 이유: 테스트가 <b>자기가 넣은 저장소</b>를 위젯이 실제로 거쳤는지를 재야 한다.
    /// 정적 계수면 다른 픽스처의 흔적이 섞여 「위젯이 경계를 우회했다」를 가를 수 없다.</para>
    /// </summary>
    public sealed class InMemoryGearMenuOnboardingSeenStore : IGearMenuOnboardingSeenStore
    {
        private bool _seen;

        public InMemoryGearMenuOnboardingSeenStore(bool seen)
        {
            _seen = seen;
        }

        /// <summary><see cref="IsSeen"/>이 불린 횟수.</summary>
        public int ReadCount { get; private set; }

        /// <summary><see cref="MarkSeen"/>이 불린 횟수.</summary>
        public int WriteCount { get; private set; }

        public bool IsSeen
        {
            get
            {
                ReadCount++;
                return _seen;
            }
        }

        public void MarkSeen()
        {
            WriteCount++;
            _seen = true;
        }
    }
}
