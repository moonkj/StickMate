using System;
using System.Reflection;
using NUnit.Framework;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>사전정의 에디터 어셈블리</b>(<c>Assembly-CSharp-Editor</c> = asmdef 없는
    /// <c>Assets/Editor/</c>)를 테스트에서 들여다보는 유일한 통로.
    ///
    /// ============================================================================
    /// 왜 리플렉션인가 — 취향이 아니라 구조다
    /// ============================================================================
    /// asmdef 기반 어셈블리(<c>StickMate.Tests.EditMode</c>)는 참조 목록에
    /// <b>사전정의 어셈블리를 적을 수 없다</b>. 의존 방향이 반대다 —
    /// <c>Assembly-CSharp-Editor</c>가 asmdef들을 참조한다(그래서 <c>Assets/Editor/</c>의 코드는
    /// <c>StickMate.Runtime</c>을 부를 수 있지만 그 반대는 못 한다).
    /// ⇒ <b>컴파일 시점 참조가 물리적으로 불가능</b>하고, 남는 것은 로드된 도메인을 뒤지는 것뿐이다.
    /// 이것이 code-inspection 발견 <b>N6</b>의 내용이다.
    ///
    /// <para>★ 그래서 이 도구를 쓰는 모든 단언은 <b>계측기 교정</b>을 함께 해야 한다 —
    /// 이름 하나 오타 나면 <see cref="FindType"/>가 <c>null</c>을 돌려주고, 그 <c>null</c>은
    /// "정말 없다"와 <b>똑같이 생겼다</b>(이 저장소가 반복해서 당한 형태).
    /// 그래서 <see cref="RequireType"/>는 못 찾으면 <b>실패</b>시키고,
    /// <see cref="FindPublicStaticMethod"/>는 있는 이름/없는 이름 양방향으로 교정할 수 있게
    /// <c>null</c>을 그대로 돌려준다.</para>
    /// </summary>
    internal static class PredefinedEditorAssemblyProbe
    {
        /// <summary>로드된 어셈블리 전체에서 전체이름으로 타입을 찾는다. 없으면 null.</summary>
        internal static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                Type t;
                try { t = loaded[i].GetType(fullName, false); }
                catch (Exception) { continue; }   // 동적/로드 실패 어셈블리
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>없으면 <b>실패</b>시킨다 — "없다"를 조용히 통과시키지 않는다.</summary>
        internal static Type RequireType(string fullName)
        {
            Type t = FindType(fullName);
            Assert.IsNotNull(t,
                $"타입 '{fullName}'을 로드된 어셈블리 어디에서도 못 찾았습니다.\n" +
                "이름이 바뀌었거나 그 파일이 컴파일되지 않았습니다. 이 감사는 리플렉션이 유일한 경로라 " +
                "이름이 틀리면 '없음'과 구분되지 않습니다 — 이름부터 확인하십시오.");
            return t;
        }

        /// <summary>공개 정적 메서드. 없으면 null(부재 단언용).</summary>
        internal static MethodInfo FindPublicStaticMethod(Type owner, string name)
        {
            if (owner == null || string.IsNullOrEmpty(name)) return null;
            return owner.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>공개 정적 상수(리터럴)의 값. 없으면 null.</summary>
        internal static object FindPublicStaticConstant(Type owner, string name)
        {
            if (owner == null || string.IsNullOrEmpty(name)) return null;
            FieldInfo f = owner.GetField(name, BindingFlags.Public | BindingFlags.Static);
            return f == null ? null : f.GetValue(null);
        }
    }
}
