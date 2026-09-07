using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// <see cref="CrispText"/> 전용 틱 호스트 — 프로세스 전체에 <b>하나만</b> 존재한다.
    ///
    /// <para><b>왜 별도 파일·별도 클래스인가</b>
    /// <list type="number">
    ///   <item>Unity는 MonoBehaviour 파일명과 클래스명이 같기를 요구한다. <c>CrispText.cs</c> 안에
    ///     두 번째 MonoBehaviour를 넣으면 그 타입의 <c>MonoScript</c>가 잡히지 않을 수 있다.</item>
    ///   <item>호스트를 <see cref="CrispText"/> 자신으로 쓰면 <b>빈 글자 그래픽</b>이 하나 더 생겨
    ///     캔버스 배치에 들어간다(<see cref="UnityEngine.UI.Text"/> 파생이므로).</item>
    ///   <item>인스턴스마다 <c>LateUpdate</c>를 두면 창 하나에 60~80개인 이 앱에서 MonoBehaviour
    ///     호출 오버헤드만 매 프레임 수십 마이크로초가 된다. <b>24시간 상주 앱</b>이라 그 상수 비용이
    ///     그냥 상수가 아니다.</item>
    /// </list></para>
    ///
    /// <para><b>이 컴포넌트가 하는 일은 한 줄이다.</b> 실제 판정은
    /// <see cref="CrispText.ReSnapIfDrifted"/>가, 산술은
    /// <see cref="Platform.GlyphPixelSnapPolicy"/>가 한다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class CrispTextSnapDriver : MonoBehaviour
    {
        private void LateUpdate() => CrispText.TickAll();
    }
}
