using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>세 번째 사본</b>을 자동으로 잡는다 — <c>Config != null ? Config.X : 리터럴</c> 형태의
    /// <b>폴백 리터럴</b>이 <c>StickConfig</c>의 실효값과 갈라진 것(2026-09-01 신설).
    ///
    /// ============================================================================
    /// 왜 이 검사가 필요했나 — 이 드리프트는 <b>화면에 안 보인다</b>
    /// ============================================================================
    /// <see cref="ConfigAssetDriftLedgerTests"/>는 "코드 기본값 vs 배포 에셋"을,
    /// <see cref="DuplicatedPoseConstantParityTests"/>는 "프리팹 베이커/초상화 상수 vs 실효값"을
    /// 본다. 그런데 <b>세 번째 사본</b>이 있다: 런타임 코드가 <c>Config == null</c>일 때를 대비해
    /// 각 호출부에 적어 둔 폴백 리터럴이다.
    ///
    /// <para>이 사본은 <b>정상 실행에서 절대 쓰이지 않으므로</b> 어긋나도 아무 증상이 없다. 그래서
    /// 조용히 낡는다. 실제로 이 라운드에 <c>States/StickmanBlackboard.cs</c> 한 파일에서만
    /// <b>여섯 개</b>가 옛 값에 멈춰 있었다(팔꿈치 122° vs 실효 98°, 머리 이동 0.035 vs 0,
    /// 활 자세 네 각도 88/93/-100/100 vs 104/108/-99/119). 리더가 지목한 것은 그중 <b>한 줄</b>이었고
    /// 나머지 다섯은 이 검사를 만들면서 나왔다 — 손으로 한 줄씩 고치는 방식의 한계가 그것이다.</para>
    ///
    /// <para>그리고 이 값들은 <b>테스트 경로에서는 실제로 쓰인다</b>(설정 에셋 없이 블랙보드를 만드는
    /// 테스트가 여럿이다). 즉 폴백이 낡으면 "테스트가 프로덕션과 다른 자세를 검증하는" 상태가 된다.</para>
    ///
    /// ============================================================================
    /// 규칙은 <see cref="ConfigAssetDriftLedgerTests"/>와 같은 어법이다
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>차이는 <b>기본이 실패</b>다. 아무것도 안 적으면 잡힌다.</item>
    ///   <item>정당한 차이는 <see cref="Ledger"/>에 <b>사유와 두 값을 함께</b> 등재하면 통과한다.
    ///     ("설정이 없으면 이 기능을 끈다" 같은 <b>의도된 0</b>이 여기 온다.)</item>
    ///   <item>대장은 <b>스스로 만료된다</b> — 등재된 항목이 더는 차이가 아니면 빨간불이다.</item>
    ///   <item>대장 항목은 <b>양쪽 값을 핀으로 박제</b>한다. 그래서 실효값이 움직이면 이미 등재된
    ///     항목이라도 빨간불이 된다.</item>
    /// </list>
    ///
    /// <para><b>비교 대상은 실효값</b>이다(배포 에셋이 이긴다 — 2026-09-01 리더 정책 판정).
    /// 폴백이 흉내 내야 하는 것은 "코드에 적힌 초기값"이 아니라 <b>실제로 돌아가는 값</b>이다.</para>
    ///
    /// <para><b>네거티브 컨트롤</b>: <see cref="네거티브_스캐너가_실제로_리터럴을_읽고_구분한다"/> —
    /// 정규식이 0건을 훑거나 항상 같은 값을 뱉으면 이 파일 전체가 "항상 참인 단언"이 된다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 소스와 에셋을 <b>읽기만</b> 한다.</para>
    /// </summary>
    public sealed class ConfigFallbackLiteralDriftTests
    {
        private const string LogPrefix = "[폴백드리프트]";
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>
        /// 훑을 루트들. ★ 2026-09-02 — <c>Assets/Editor</c>가 추가됐다.
        ///
        /// <para>왜: 종전 루트는 <c>_Project/Scripts</c> 하나였는데, <b>프리팹/씬을 굽는 코드가 그
        /// 밖에 있다</b>(<c>Assets/Editor/SceneBootstrapper.cs</c>). 그쪽 폴백은 "이 캐릭터의 몸이
        /// 실제로 어떤 물리로 구워지는가"를 정하므로 여기서 낡으면 <b>화면에 바로 나타난다</b> —
        /// 런타임 폴백보다 오히려 더 위험한 자리인데 감시망 밖이었다. 실측해 보니 지금은 드리프트
        /// 0건이지만(그쪽 <c>gravityScale</c> 폴백은 이미 실효값 3f다), <b>감시망 밖이라는 사실
        /// 자체가 위험</b>이라 닫는다.</para>
        /// </summary>
        private static string[] ScanRoots => new[]
        {
            Path.Combine(Application.dataPath, "_Project", "Scripts"),
            Path.Combine(Application.dataPath, "Editor"),
        };

        /// <summary>
        /// <c>X != null ? X.field : 리터럴</c>. 역참조 이름(<c>Config</c> / <c>_blackboard.Config</c> /
        /// <c>config</c> …)이 <b>양쪽에서 같아야</b> 매치되도록 역참조(<c>\1</c>)로 묶는다 — 그래야
        /// "다른 객체의 필드"를 잘못 물지 않는다.
        /// </summary>
        private static readonly Regex FallbackPattern = new Regex(
            @"([A-Za-z_][\w.]*)\s*!=\s*null\s*\?\s*\1\.([A-Za-z_]\w*)\s*:\s*(-?[\d.]+f?|true|false)",
            RegexOptions.Compiled);

        /// <summary>
        /// ★ 2026-09-02 신설 — <b>람다 셀렉터 폴백</b>: <c>Helper(c =&gt; c.field, 리터럴)</c>.
        ///
        /// <para>왜 필요했나: 위 삼항 정규식은 이 형태를 <b>한 건도 못 본다</b>. 실측 결과 소스에
        /// 이 형태가 25건 있었고 그중 <b>3건이 실제 드리프트</b>였다 —
        /// <c>ArcheryRenderer.archeryOutroSeconds</c>(0.75 vs 0.55, 같은 값을 쓰는
        /// <c>ArcheryState</c> 쪽의 쌍둥이였다), <c>AutoWanderController.stepUpChance</c>(0.5 vs 0.85),
        /// <c>AutoWanderController.stepUpMaxHeight</c>(1.5 vs 2.4, 개명으로 자연 해소).
        /// 즉 검사가 세워진 첫날부터 <b>실제 드리프트 3건이 정규식 사각지대에 앉아 있었다.</b></para>
        ///
        /// <para>람다 파라미터 이름은 자유롭게 두되(<c>c</c>/<c>cfg</c>/<c>x</c>), 역참조로 묶어
        /// <b>같은 이름</b>일 때만 문다 — 그래야 다른 객체의 필드를 잘못 물지 않는다(삼항 쪽과 같은
        /// 안전장치다).</para>
        /// </summary>
        private static readonly Regex LambdaFallbackPattern = new Regex(
            @"\(\s*([A-Za-z_]\w*)\s*=>\s*\1\.([A-Za-z_]\w*)\s*,\s*(-?[\d.]+f?|true|false)\s*\)",
            RegexOptions.Compiled);

        /// <summary>한 줄이 곧 "이 차이는 의도된 것이며, 두 값은 지금 이 값이다"라는 계약이다.</summary>
        private readonly struct Drift
        {
            public readonly string RelativePath;
            public readonly string Field;
            /// <summary>소스에 적힌 폴백 리터럴(핀).</summary>
            public readonly float Literal;
            /// <summary>StickConfig 실효값(핀).</summary>
            public readonly float Effective;
            public readonly string Reason;

            public Drift(string relativePath, string field, float literal, float effective, string reason)
            {
                RelativePath = relativePath;
                Field = field;
                Literal = literal;
                Effective = effective;
                Reason = reason;
            }

            public string Key => RelativePath + "::" + Field;
        }

        /// <summary>
        /// ★ 의도된 차이 대장 — 스캐너를 세운 시점의 현황을 있는 그대로 박제해, <b>새로 생기는
        /// 드리프트만</b> 빨간불이 되게 한다.
        ///
        /// <para><b>8건 전부 소스를 읽어 의도를 확인했다</b>(센티널 / 로그 자리표시자 / 기능 스위치).
        /// 스캐너를 세운 시점에 함께 나왔던 "★미검토" 5건
        /// (<c>GroundSensor.groundSnapTolerance</c> / <c>GroundSensor.gravityScale</c> /
        /// <c>ParkourClimbState.parkourClimbDuration</c> / <c>ArcheryState.archeryOutroSeconds</c> /
        /// <c>ArcheryState.walkStrideScale</c>)는 2026-09-02 라운드에서 <b>전부 낡은 사본으로 확정</b>돼
        /// 실효값으로 고쳐졌고, 그래서 대장에서 내려갔다 — 의도된 차이가 아니었기 때문이다.
        /// 판정 근거는 각 소스의 수정 지점 주석에 있다.</para>
        /// </summary>
        private static readonly Drift[] Ledger =
        {
            // ---- 의도가 소스에 명시돼 있는 것(읽고 확인함) ----
            new Drift("Interaction/SparkleCadence.cs", "wanderIdleDurationMax", 0f, 6f,
                "0은 값이 아니라 **센티널**이다 — 바로 다음 줄이 `configured > 0.01f ? configured : " +
                "FallbackIdleWindowSeconds(6f)`로 걸러 실효값과 같은 6초로 되돌린다. 의도 확인함."),
            new Drift("Interaction/GraffitiDirector.cs", "graffitiMinRadiusPx", 0f, 200f,
                "**로그 문자열 안의 자리표시자**다(건너뜀 사유를 찍는 Debug.Log). 판정에 쓰이지 않는다."),
            new Drift("Interaction/GraffitiDirector.cs", "graffitiMaxRadiusPx", 0f, 300f, "위와 같은 로그 자리표시자."),
            new Drift("Interaction/StressGaugeRenderer.cs", "stressTierCautionLevel", 0.4f, 2f,
                "실효값 2는 \"1 초과 = 이 단계를 조용히 끈다\"는 **스위치**이고(바로 위 줄이 " +
                "`rawCaution > 1f`로 거른다), 폴백 0.4는 그 기능을 켰을 때의 원래 기본값이다. " +
                "소스 주석이 \"0.4로 되돌리면 기존 경로를 100% 그대로 탄다\"고 명시한다. 의도 확인함."),
            new Drift("Interaction/CharacterProgressionDirector.cs", "progressionPassiveXpPerMinute", 0f, 1.5f,
                "\"설정이 없으면 XP를 주지 않는다\" — Grant() 호출부가 `> 0f`로 거르는 형태라 " +
                "0이 곧 비활성이다. 의도 확인함."),
            new Drift("Interaction/CharacterProgressionDirector.cs", "progressionBattleWinXp", 0f, 25f,
                "위와 같다(Grant(0)은 무보상)."),
            new Drift("Interaction/CharacterProgressionDirector.cs", "progressionBullseyeXp", 0f, 15f, "위와 같다."),
            new Drift("Core/AppSettingsModel.cs", "dialogueMinVisibleSeconds", 0.7f, 0f,
                "구판 고정 하한 0.7초. 규칙 4-b(2026-09-01) 이후 실제 하한은 글자수 비례 가독예산이 " +
                "정하고 이 필드는 그 위에 얹는 절대 하한일 뿐이라, Config가 없는 경로에서 0.7이 " +
                "남아 있어도 가독예산보다 짧아 무해하다."),

            // ---- ★ 2026-09-02 판정 완료 — 이 자리에 있던 미검토 5건은 전부 **낡은 사본**으로 확정돼 고쳤다 ----
            //
            // States/GroundSensor.cs      groundSnapTolerance 6 -> 20 (3곳) / gravityScale 1 -> 3 (1곳)
            // States/ParkourClimbState.cs parkourClimbDuration 0.5 -> 1.20 (2곳, 실효값이 라운드 도중 1.05 -> 1.20으로 또 움직였다)
            // States/ArcheryState.cs      archeryOutroSeconds 0.75 -> 0.55 / walkStrideScale 1 -> 0.93
            //
            // 판정 근거는 각 소스의 수정 지점 주석에 남겼다. 요약하면 셋 다 "의도된 폴백"이 아니었다:
            //   · 6과 0.5는 각각 2026-08-30 / 2026-09-01 라운드가 코드 기본값·에셋을 올릴 때 함께
            //     따라오지 못한 잔여 사본이다.
            //   · 1(gravityScale) / 1(walkStrideScale) / 0.75(archeryOutro)는 **어느 시점에도 이 프로젝트의
            //     값이었던 적이 없다** — 처음부터 틀린 사본이라 "옛 기본값"조차 아니었다.
            //
            // 그래서 이 대장에는 지금 **의도된 차이 8건만** 남는다. 새로 생기는 것은 전부 빨간불이다.

            // ---- ★★ 2026-09-02 인계 1건 — "의도된 차이"가 아니라 **편집 권한 경계** 때문이다 ----
            // (앞 라운드가 나에게 넘겼던 것과 정확히 같은 어법으로, 이번에는 내가 넘긴다.)
            new Drift("States/AutoWanderController.cs", "stepUpChance", 0.5f, 0.85f,
                "★인계(디버거 -> 리더). 이 라운드에 신설한 **람다 셀렉터 폴백** 정규식이 처음으로 " +
                "잡아낸 드리프트다(종전 정규식은 `Cfg(c => c.stepUpChance, 0.5f)` 형태를 한 건도 " +
                "보지 못했다). 0.5는 옛 코드 기본값이고 실효값은 0.85다.\n" +
                "고치지 않은 이유는 값이 애매해서가 아니라 **파일을 만질 수 없어서**다 — 리더 지시: " +
                "\"AutoWanderController.cs는 앞 라운드가 방금 고쳤다. mtime을 확인하고 최근이면 " +
                "보고만 해라\". 착수 시점 mtime이 00:33(약 10분 전)이라 보고만 한다.\n" +
                "이것은 의도된 차이가 **아니다**. 그 파일을 만질 수 있게 되는 순간 0.5f -> 0.85f로 " +
                "고치고 이 줄을 지워야 한다(안 지우면 '더 이상 차이가 아님' 쪽 논리로 이 검사가 " +
                "스스로 빨간불이 되어 알려준다).\n" +
                "영향: 되올라가기 발동 확률이 Config 없는 경로에서만 0.85 -> 0.5로 떨어진다. " +
                "이 분기는 \"한 번 Dock 아래로 내려간 캐릭터가 영영 못 올라오는\" 것을 막는 " +
                "유일한 경로라(그 파일 주석), 확률이 낮아지면 복귀가 느려진다."),

            // ---- ★★ 인계 항목(coder -> 디버거) 처리 완료: 2026-09-02 ----
            // coder가 등반 박자 라운드에서 코드 기본값·에셋을 1.05 -> 1.20으로 올렸지만
            // States/ParkourClimbState.cs가 그 라운드의 편집 금지 파일이라 폴백 두 곳을 못 고치고
            // 여기 한 줄로 인계해 두었다. 그 파일의 편집 권한을 가진 이 라운드에서 두 곳을 1.20f로
            // 맞췄으므로 인계 줄을 지운다(그쪽이 남긴 처리 지시 그대로다).
            //
            // ★ 이 인계 자체가 이번 조사의 결론을 실물로 증명한다: 폴백 리터럴은 **하루 안에도**
            //   낡는다. 0.5 -> 1.05 -> 1.20이 전부 같은 날 안에서 일어났고, 그중 1.05 -> 1.20은
            //   이 조사가 도는 **도중에** 벌어졌다.
        };

        // ============================================================================
        // 스캐너
        // ============================================================================

        private readonly struct Hit
        {
            public readonly string RelativePath;
            public readonly int Line;
            public readonly string Field;
            public readonly string LiteralText;

            public Hit(string relativePath, int line, string field, string literalText)
            {
                RelativePath = relativePath;
                Line = line;
                Field = field;
                LiteralText = literalText;
            }

            public string Key => RelativePath + "::" + Field;
        }

        private static List<Hit> ScanAll()
        {
            var hits = new List<Hit>();
            foreach (string root in ScanRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (normalized.Contains("/Tests/")) continue; // 테스트 자신은 대상이 아니다.

                    string source = File.ReadAllText(path);
                    string relative = ToLedgerPath(normalized);
                    AddMatches(hits, FallbackPattern, source, relative);
                    AddMatches(hits, LambdaFallbackPattern, source, relative);
                }
            }
            return hits;
        }

        /// <summary>
        /// 대장에 적는 경로 표기. <c>_Project/Scripts</c> 아래는 종전 그대로
        /// (<c>States/GroundSensor.cs</c>) 두고, 그 밖(<c>Assets/Editor</c>)은 <c>Assets/</c> 기준
        /// (<c>Editor/SceneBootstrapper.cs</c>)으로 적는다 — 루트를 늘리면서 <b>이미 등재된 8건의
        /// 키가 바뀌지 않게</b> 하기 위한 것이다(키가 바뀌면 대장이 통째로 "소스에서 못 찾음"이 된다).
        /// </summary>
        private static string ToLedgerPath(string normalized)
        {
            int scripts = normalized.IndexOf("/Scripts/", StringComparison.Ordinal);
            if (scripts >= 0) return normalized.Substring(scripts + "/Scripts/".Length);
            int assets = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
            if (assets >= 0) return normalized.Substring(assets + "/Assets/".Length);
            return normalized;
        }

        private static void AddMatches(List<Hit> hits, Regex pattern, string source, string relative)
        {
            foreach (Match m in pattern.Matches(source))
            {
                int line = 1;
                for (int i = 0; i < m.Index; i++) if (source[i] == '\n') line++;
                hits.Add(new Hit(relative, line, m.Groups[2].Value, m.Groups[3].Value));
            }
        }

        /// <summary>리터럴 텍스트 -> 숫자. bool은 1/0으로 환산해 float 한 축에서 비교한다.</summary>
        private static bool TryParseLiteral(string text, out float value)
        {
            if (text == "true") { value = 1f; return true; }
            if (text == "false") { value = 0f; return true; }
            return float.TryParse(text.TrimEnd('f'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>배포 에셋이 실제로 들고 있는 값(= 런타임이 쓰는 값). 숫자를 베끼지 않는다.</summary>
        private static bool TryReadEffective(StickConfig config, string fieldName, out float value)
        {
            value = 0f;
            FieldInfo f = typeof(StickConfig).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return false; // StickConfig 필드가 아닌 동명 프로퍼티 — 이 검사의 대상이 아니다.

            object raw = f.GetValue(config);
            if (raw is float fv) { value = fv; return true; }
            if (raw is int iv) { value = iv; return true; }
            if (raw is bool bv) { value = bv ? 1f : 0f; return true; }
            return false; // enum/Color/Vector 등은 리터럴 폴백 형태가 아니다.
        }

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}");
            return config;
        }

        // ============================================================================
        // 본 검사
        // ============================================================================

        [Test]
        public void 등재되지_않은_폴백_리터럴_드리프트가_없다()
        {
            StickConfig config = LoadDeployedConfig();
            var ledger = new Dictionary<string, Drift>();
            foreach (Drift d in Ledger) ledger[d.Key] = d;

            var unregistered = new List<string>();
            var stalePins = new List<string>();
            var seenLedgerKeys = new HashSet<string>();
            int compared = 0;

            foreach (Hit hit in ScanAll())
            {
                if (!TryParseLiteral(hit.LiteralText, out float literal)) continue;
                if (!TryReadEffective(config, hit.Field, out float effective)) continue;
                compared++;

                bool differs = Mathf.Abs(literal - effective) > 1e-4f;

                if (ledger.TryGetValue(hit.Key, out Drift entry))
                {
                    seenLedgerKeys.Add(hit.Key);
                    if (!differs)
                    {
                        stalePins.Add($"  · {hit.RelativePath}:{hit.Line} {hit.Field} — 더 이상 차이가 " +
                                      "아닙니다(둘 다 " + literal.ToString("0.###") + "). 대장에서 지우세요.");
                        continue;
                    }
                    if (Mathf.Abs(entry.Literal - literal) > 1e-4f || Mathf.Abs(entry.Effective - effective) > 1e-4f)
                    {
                        stalePins.Add($"  · {hit.RelativePath}:{hit.Line} {hit.Field} — 대장에 박힌 핀" +
                                      $"({entry.Literal:0.###} vs {entry.Effective:0.###})이 실제" +
                                      $"({literal:0.###} vs {effective:0.###})와 다릅니다. " +
                                      "값이 움직였으니 등재 사유가 아직 유효한지 다시 판단하세요.");
                    }
                    continue;
                }

                if (differs)
                {
                    unregistered.Add($"  · {hit.RelativePath}:{hit.Line} — " +
                                     $"폴백 {literal:0.###} 인데 StickConfig.{hit.Field}의 실효값은 {effective:0.###} 입니다.");
                }
            }

            foreach (Drift d in Ledger)
            {
                if (!seenLedgerKeys.Contains(d.Key))
                {
                    stalePins.Add($"  · {d.RelativePath} {d.Field} — 대장에 있는데 소스에서 찾지 못했습니다" +
                                  "(코드가 지워졌거나 형태가 바뀌었습니다). 대장에서 지우세요.");
                }
            }

            var ledgerLines = new List<string>();
            foreach (Drift d in Ledger)
            {
                ledgerLines.Add($"  · {d.RelativePath} {d.Field}: 폴백 {d.Literal:0.###} vs 실효 " +
                                $"{d.Effective:0.###} — {d.Reason}");
            }
            Debug.Log($"{LogPrefix} 폴백 리터럴 {compared}건 대조 — 등재 {Ledger.Length}건, " +
                      $"미등재 차이 {unregistered.Count}건, 낡은 핀 {stalePins.Count}건.\n" +
                      "등재된 의도된 차이:\n" + string.Join("\n", ledgerLines));

            Assert.IsEmpty(unregistered,
                $"{LogPrefix} <b>등재되지 않은</b> 폴백 리터럴 드리프트가 있습니다.\n" +
                string.Join("\n", unregistered) +
                "\n\n폴백 리터럴은 Config == null 경로에서만 쓰이므로 어긋나도 화면에 아무 증상이 " +
                "없습니다 — 그래서 조용히 낡습니다. 실효값과 맞추거나, 의도된 차이라면 사유와 함께 " +
                "Ledger에 등재하세요.");

            Assert.IsEmpty(stalePins,
                $"{LogPrefix} 대장이 낡았습니다(등재 항목이 현실과 어긋납니다).\n" + string.Join("\n", stalePins));
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 스캐너가 실제로 소스를 읽고 <b>서로 다른 값을 구분</b>하는지.
        /// 정규식이 아무것도 못 물거나(0건) 항상 같은 숫자를 뱉으면 위 검사는 통과할 수밖에 없다.
        /// </summary>
        [Test]
        public void 네거티브_스캐너가_실제로_리터럴을_읽고_구분한다()
        {
            List<Hit> hits = ScanAll();
            Assert.Greater(hits.Count, 100,
                $"{LogPrefix} 폴백 패턴을 {hits.Count}건밖에 못 찾았습니다 — 정규식이 깨졌거나 경로가 " +
                "틀렸습니다. 0건을 훑고 초록불을 내는 것이 이 검사가 할 수 있는 최악의 일입니다.");

            var distinctLiterals = new HashSet<string>();
            foreach (Hit h in hits) distinctLiterals.Add(h.LiteralText);
            Assert.Greater(distinctLiterals.Count, 10,
                $"{LogPrefix} 읽어낸 리터럴 종류가 {distinctLiterals.Count}가지뿐입니다 — " +
                "캡처 그룹이 값을 제대로 안 물고 있을 수 있습니다.");

            // 이 라운드에 고친 그 줄이 실제로 스캐너에 잡히고, 이제 실효값과 같은지 직접 확인한다.
            StickConfig config = LoadDeployedConfig();
            Assert.IsTrue(TryReadEffective(config, "idleAmbientLookElbowDegrees", out float effectiveElbow));

            bool found = false;
            foreach (Hit h in hits)
            {
                if (h.Field != "idleAmbientLookElbowDegrees") continue;
                found = true;
                Assert.IsTrue(TryParseLiteral(h.LiteralText, out float literal));
                Assert.AreEqual(effectiveElbow, literal, 1e-4f,
                    $"{LogPrefix} {h.RelativePath}:{h.Line} — 리더가 지목한 '세 번째 사본'이 아직 " +
                    "실효값과 다릅니다.");
            }
            Assert.IsTrue(found,
                $"{LogPrefix} idleAmbientLookElbowDegrees 폴백을 스캐너가 못 찾았습니다 — " +
                "스캐너가 그 파일을 안 보고 있다는 뜻입니다.");
        }

        /// <summary>
        /// ★ 2026-09-02 네거티브 컨트롤 — <b>새로 추가한 람다 셀렉터 정규식</b>이 실제로 무는가.
        ///
        /// <para>이 검사가 없으면, 정규식을 오타 내서 <b>0건을 무는</b> 상태로 두어도 본 검사는
        /// 초록이다("드리프트 없음"). 오늘 밤 이 저장소에서 거짓 초록이 다섯 건 나온 이유가 정확히
        /// 그 형태였다. 그래서 (a) 이 형태를 실제로 몇 건 물었는지, (b) 그중 하나가 <b>이 라운드에
        /// 고친 그 줄</b>인지를 직접 확인한다.</para>
        /// </summary>
        [Test]
        public void 네거티브_람다_셀렉터_폴백을_실제로_문다()
        {
            // 합성 입력으로 정규식 자체를 먼저 검증한다 — 소스가 어떻게 바뀌어도 이 단언은 유지된다.
            Match good = LambdaFallbackPattern.Match("ConfigFloat(c => c.archeryOutroSeconds, 0.55f)");
            Assert.IsTrue(good.Success, $"{LogPrefix} 표준형을 못 물었습니다 — 정규식이 깨졌습니다.");
            Assert.AreEqual("archeryOutroSeconds", good.Groups[2].Value);
            Assert.AreEqual("0.55f", good.Groups[3].Value);

            // ★ 역참조가 실제로 일하는지 — 람다 파라미터와 역참조 대상이 **다른 이름**이면 물면 안 된다
            //   (다른 객체의 필드를 잘못 무는 것을 막는 장치다).
            Assert.IsFalse(LambdaFallbackPattern.IsMatch("Helper(c => other.someField, 0.5f)"),
                $"{LogPrefix} 역참조가 일하지 않습니다 — 다른 객체의 필드를 물고 있습니다.");
            // 리터럴이 아니면(다른 식) 물지 않는다.
            Assert.IsFalse(LambdaFallbackPattern.IsMatch("Helper(c => c.field, other.field)"));

            // 그리고 **실제 소스에서** 이 형태를 한 건이라도 물어야 한다. 0건이면 이 정규식을 추가한
            // 의미가 없고, 위 합성 단언만 통과하는 "장식용 정규식"이 된다.
            int lambdaHits = 0;
            foreach (string root in ScanRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (path.Replace('\\', '/').Contains("/Tests/")) continue;
                    lambdaHits += LambdaFallbackPattern.Matches(File.ReadAllText(path)).Count;
                }
            }
            Assert.Greater(lambdaHits, 5,
                $"{LogPrefix} 소스에서 람다 셀렉터 폴백을 {lambdaHits}건밖에 못 찾았습니다 — " +
                "이 정규식을 추가한 이유(실측 25건, 그중 3건이 실제 드리프트)가 사라졌습니다.");

            // 이 라운드에 고친 그 줄이 스캐너 시야에 있고, 이제 실효값과 같은지 직접 본다.
            StickConfig config = LoadDeployedConfig();
            Assert.IsTrue(TryReadEffective(config, "archeryOutroSeconds", out float effectiveOutro));
            bool sawRenderer = false;
            foreach (Hit h in ScanAll())
            {
                if (h.RelativePath != "Interaction/ArcheryRenderer.cs" || h.Field != "archeryOutroSeconds") continue;
                sawRenderer = true;
                Assert.IsTrue(TryParseLiteral(h.LiteralText, out float literal));
                Assert.AreEqual(effectiveOutro, literal, 1e-4f,
                    $"{LogPrefix} {h.RelativePath}:{h.Line} — States/ArcheryState.cs가 쓰는 것과 " +
                    "**같은 값**인데 이쪽만 어긋났습니다. 그러면 Config 없는 경로에서 상태와 연출이 " +
                    "서로 다른 시각에 사이클을 끝냅니다.");
            }
            Assert.IsTrue(sawRenderer,
                $"{LogPrefix} ArcheryRenderer의 람다 폴백을 스캐너가 못 찾았습니다 — 새 정규식이 " +
                "실제 소스에서는 일하지 않는다는 뜻입니다(합성 단언만 통과한 것).");
        }

        /// <summary>
        /// ★ 2026-09-02 네거티브 컨트롤 — <b>새로 추가한 <c>Assets/Editor</c> 루트</b>를 실제로
        /// 훑는가.
        ///
        /// <para>루트를 늘려 놓고 경로가 틀려 <b>0개 파일</b>을 훑어도 본 검사는 초록이다. 그 형태가
        /// 이 도구가 낼 수 있는 최악의 결과라, 파일이 실제로 읽히는지와 <b>그 안의 폴백을 실제로
        /// 무는지</b>를 함께 확인한다.</para>
        ///
        /// <para>표적은 <c>Editor/SceneBootstrapper.cs</c>의 <c>gravityScale</c> 폴백이다. 이 값은
        /// 이번 라운드 조사에서 <b>결정적 증거</b>였다 — <c>States/GroundSensor.cs</c>의 폴백 1f가
        /// "설정이 없으면 몸의 중력도 Unity 기본 1"이라는 해석으로 정당화될 수 있는지 물었을 때,
        /// <b>몸을 굽는 코드가 같은 자리에서 3f를 쓴다</b>는 사실이 그 해석을 반증했다.</para>
        /// </summary>
        [Test]
        public void 네거티브_에디터_루트를_실제로_훑는다()
        {
            string editorRoot = Path.Combine(Application.dataPath, "Editor");
            Assert.IsTrue(Directory.Exists(editorRoot), $"{LogPrefix} Editor 루트가 없습니다: {editorRoot}");

            int files = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories).Length;
            Assert.Greater(files, 0, $"{LogPrefix} Editor 루트에서 .cs를 0개 훑었습니다 — 빈 스캔입니다.");

            bool sawEditorHit = false;
            foreach (Hit h in ScanAll())
            {
                if (!h.RelativePath.StartsWith("Editor/", StringComparison.Ordinal)) continue;
                sawEditorHit = true;
                break;
            }
            Assert.IsTrue(sawEditorHit,
                $"{LogPrefix} Editor 루트의 .cs를 {files}개 훑었는데 폴백을 한 건도 못 물었습니다 — " +
                "경로 표기(ToLedgerPath)나 정규식이 그쪽에서 일하지 않는다는 뜻입니다.");

            // 프리팹 베이커의 중력 폴백이 **실효값과 같은지**를 직접 본다(이번 조사의 결정적 증거).
            StickConfig config = LoadDeployedConfig();
            Assert.IsTrue(TryReadEffective(config, "gravityScale", out float effectiveGravity));
            bool sawBaker = false;
            foreach (Hit h in ScanAll())
            {
                if (h.RelativePath != "Editor/SceneBootstrapper.cs" || h.Field != "gravityScale") continue;
                sawBaker = true;
                Assert.IsTrue(TryParseLiteral(h.LiteralText, out float literal));
                Assert.AreEqual(effectiveGravity, literal, 1e-4f,
                    $"{LogPrefix} {h.RelativePath}:{h.Line} — **프리팹 베이커**의 중력 폴백이 실효값과 " +
                    "다릅니다. 여기가 낡으면 런타임 폴백과 달리 **화면에 바로 나타납니다**(몸이 그 " +
                    "중력으로 구워집니다).");
            }
            Assert.IsTrue(sawBaker,
                $"{LogPrefix} Editor/SceneBootstrapper.cs의 gravityScale 폴백을 못 찾았습니다.");
        }
    }
}
