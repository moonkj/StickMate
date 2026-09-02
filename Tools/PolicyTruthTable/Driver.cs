// 검증팀 도구 — 프로덕션 ReservedBarRevealPolicy.cs 를 한 줄도 고치지 않고 그대로 컴파일해
// 입력 전조합(56가지)을 실행하고 안전 불변식을 실측한다. 규칙을 여기 다시 적지 않는다
// (두 벌로 적으면 두 자가 갈라진다) — 대신 "이 순서가 아니면 위험하다"는 불변식만 단언한다.
using System;
using StickMate.Platform;

internal static class Driver
{
    private static int _fail;
    private static int _cases;

    private static void Inv(bool ok, string tag)
    {
        _cases++;
        if (!ok) { _fail++; Console.WriteLine("  !! 위반  " + tag); }
    }

    private static string P(ReservedBarPlan p)
        => $"sys={(p.WriteSystem ? p.SystemAutoHideValue.ToString() : "-")} trace={(p.WriteTrace ? "W" : "-")}{(p.CloseTrace ? "C" : "-")} {p.Reason}";

    private static void Main()
    {
        bool[] B = { false, true };
        bool negctrl = Environment.GetEnvironmentVariable("NEGCTRL") == "1";

        Console.WriteLine("== ResolveStartup (2^3=8) ==");
        foreach (bool avail in B) foreach (bool obs in B) foreach (bool ok in B)
        {
            var p = ReservedBarRevealPolicy.ResolveStartup(avail, obs, ok);
            Console.WriteLine($"  avail={avail,-5} 자동숨김={obs,-5} 조회성공={ok,-5} -> {P(p)}");
            // INV-1 시스템을 바꾸려면 반드시 흔적을 먼저 쓴다.
            Inv(!p.WriteSystem || p.WriteTrace, $"startup({avail},{obs},{ok}) 흔적 없이 시스템 변경");
            // INV-2 "우리가 안 바꿨으면 디스크도 안 건드린다"
            if (avail && ok && !obs)
                Inv(!p.WriteSystem && !p.WriteTrace && !p.CloseTrace,
                    $"startup({avail},{obs},{ok}) 자동숨김 꺼짐인데 뭔가를 씀");
            // INV-3 모르면 건드리지 않는다
            if (!avail || !ok)
                Inv(!p.WriteSystem && !p.WriteTrace && !p.CloseTrace,
                    $"startup({avail},{obs},{ok}) 조회 실패인데 뭔가를 씀");
            // INV-4 해제는 반드시 '자동숨김 끄기(false)' 방향이어야 한다
            if (p.WriteSystem) Inv(p.SystemAutoHideValue == false,
                $"startup({avail},{obs},{ok}) 시작 시 자동숨김을 켜려 함");
        }

        Console.WriteLine("== ResolveRecovery (2^5=32) ==");
        foreach (bool has in B) foreach (bool orig in B) foreach (bool avail in B)
        foreach (bool obs in B) foreach (bool ok in B)
        {
            var p = ReservedBarRevealPolicy.ResolveRecovery(has, orig, avail, obs, ok);
            Console.WriteLine($"  흔적={has,-5} 원래값={orig,-5} avail={avail,-5} 관측={obs,-5} 조회성공={ok,-5} -> {P(p)}");
            // INV-5 복구는 새 흔적을 만들지 않는다(빚을 갚는 쪽이다)
            Inv(!p.WriteTrace, $"recovery({has},{orig},{avail},{obs},{ok}) 복구가 새 흔적을 씀");
            // INV-6 못 읽거나 능력 없으면 흔적을 닫지 않는다(복구 기회 소실 금지)
            if (has && (!avail || !ok))
                Inv(!p.CloseTrace && !p.WriteSystem,
                    $"recovery({has},{orig},{avail},{obs},{ok}) 조회 실패인데 흔적을 닫거나 씀");
            // INV-7 복구가 시스템에 쓴다면 반드시 흔적이 기록한 '원래 값'으로만
            if (p.WriteSystem) Inv(p.SystemAutoHideValue == orig,
                $"recovery({has},{orig},{avail},{obs},{ok}) 원래값이 아닌 값으로 복구");
            // INV-8 흔적이 없으면 아무 것도 안 한다
            if (!has) Inv(!p.WriteSystem && !p.WriteTrace && !p.CloseTrace,
                $"recovery({has},...) 흔적 없는데 뭔가를 씀");
        }

        Console.WriteLine("== ResolveQuit (2^4=16) ==");
        foreach (bool changed in B) foreach (bool orig in B) foreach (bool obs in B) foreach (bool ok in B)
        {
            var p = ReservedBarRevealPolicy.ResolveQuit(changed, orig, obs, ok);
            Console.WriteLine($"  우리가바꿈={changed,-5} 원래값={orig,-5} 관측={obs,-5} 조회성공={ok,-5} -> {P(p)}");
            // INV-9 우리가 안 바꿨으면 종료 시 아무 것도 안 한다
            if (!changed) Inv(!p.WriteSystem && !p.WriteTrace && !p.CloseTrace,
                $"quit({changed},{orig},{obs},{ok}) 안 바꿨는데 뭔가를 씀");
            // INV-10 우리가 바꿨으면 흔적은 반드시 닫힌다(빚이 영원히 남지 않게)
            if (changed) Inv(p.CloseTrace, $"quit({changed},{orig},{obs},{ok}) 빚을 안 갚고 끝냄");
            // INV-11 종료 시 쓰는 값은 반드시 사용자의 원래 값
            if (p.WriteSystem) Inv(p.SystemAutoHideValue == orig,
                $"quit({changed},{orig},{obs},{ok}) 원래값이 아닌 값으로 복원");
            // INV-12 조회 실패해도 원복은 시도한다(종료는 다시 오지 않는다)
            if (changed && !ok) Inv(p.WriteSystem, $"quit({changed},{orig},{obs},{ok}) 조회 실패로 원복 포기");
        }

        if (negctrl)
        {
            // ★ 네거티브 컨트롤 — 이 하니스가 실제로 무는지 확인한다.
            //   반드시 거짓인 불변식을 하나 넣어 본다.
            var p = ReservedBarRevealPolicy.ResolveStartup(true, true, true);
            Inv(!p.WriteSystem, "NEGCTRL: 해제 계획이 시스템을 안 바꾼다(일부러 거짓)");
        }

        Console.WriteLine($"\n단언 {_cases}건 / 위반 {_fail}건");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
