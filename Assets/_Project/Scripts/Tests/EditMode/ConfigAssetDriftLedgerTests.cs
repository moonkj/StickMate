using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>의도된 차이 대장</b> — 배포 튜닝 에셋(<c>DefaultStickConfig.asset</c>)의 직렬화 값이
    /// <see cref="StickConfig"/>의 코드 기본값과 갈라진 것을 <b>등재되지 않은 차이</b>로 잡는다
    /// (2026-09-01 "등반 4박자 라운드가 사용자 화면에 존재하지 않았다" 사고 재발 방지).
    ///
    /// ============================================================================
    /// 무슨 일이 있었나
    /// ============================================================================
    /// 저녁 라운드가 <b>코드 기본값</b>을 바꿨는데 <b>에셋을 아무도 안 고쳤다</b>. ScriptableObject는
    /// 직렬화된 값이 필드 초기자를 <b>이기므로</b>, 실제로 돌아간 것은 옛 값이었다.
    /// <list type="bullet">
    ///   <item><c>parkourClimbDuration</c> 코드 1.05 vs 에셋 <b>0.5</b> — 등반이 절반 속도로 돌았다.
    ///         0.5초에 네 박자를 넣으면 박자당 0.06~0.09초인데, 그 코드 자신의 툴팁이
    ///         "0.19초 = 사람이 한 동작으로 인지하는 하한"이라고 적어 두었다.</item>
    ///   <item><c>dialogueMinVisibleSeconds</c> 코드 0 vs 에셋 <b>0.7</b> — "가뿐하네"가 상태보다
    ///         0.32초 오래 살아남아 <b>절대 불변 원칙 1(행동-텍스트 싱크)</b>을 계속 위반했다.
    ///         리더가 "해소됐다"고 판정했던 건이 실제로는 미해소였다.</item>
    ///   <item><c>landingCrouchDeepFallHeights</c> 코드 3.02 vs 에셋 <b>3</b>.</item>
    /// </list>
    ///
    /// <b>왜 조용히 통과했는가</b> — 그날 새로 만든 필드 수십 개는 <b>에셋에 아예 없어서</b> 코드
    /// 기본값이 살아남아 정상 동작했다. 즉 "새로 만든 건 다 멀쩡한데 숫자만 바꾼 옛 필드 셋만 죽었다."
    /// 새 기능이 전부 눈에 보이니 아무도 의심하지 않았다. 재발 비용은 <b>몰라서가 아니라 발견이
    /// 늦어서</b>였다.
    ///
    /// ============================================================================
    /// 왜 "전부 같아야 한다"가 아닌가 — 그건 틀린 답이다
    /// ============================================================================
    /// 에셋 값이 코드 기본값과 다른 것 <b>자체는 정상</b>이다. 튜닝 에셋의 존재 이유가 그것이다.
    /// "전부 같아야 한다"로 잠그면 <b>정당한 튜닝을 전부 막는다</b>. 그래서 이 저장소가 이미 쓰는
    /// 어법을 따른다 — <see cref="AccessoryRuleOneCoverageTests"/>의 면제 대장이다.
    /// <list type="bullet">
    ///   <item>차이는 <b>기본이 실패</b>다. 아무것도 안 적으면 잡힌다(구멍이 안 생긴다).</item>
    ///   <item>정당한 튜닝은 <see cref="Ledger"/>에 <b>사유와 두 값을 함께</b> 등재하면 통과한다.</item>
    ///   <item>대장은 <b>스스로 만료된다</b>. 등재된 항목이 더는 차이가 아니면(에셋을 코드 기본값으로
    ///         되돌렸는데 대장에서 지우는 걸 잊었으면) 빨간불이다.</item>
    ///   <item>★ 대장 항목은 <b>양쪽 값을 핀으로 박제</b>한다. 그래서 <b>코드 기본값이 움직이면</b>
    ///         이미 등재된 필드라도 빨간불이 된다 — 이번 사고가 정확히 그 형태다. 등재만 해 두고
    ///         "이 필드는 원래 다른 값"이라며 영원히 덮는 길을 막는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 규칙은 <b>하나</b>다 — "에셋이 언제나 이긴다" (2026-09-01 리더 정책 판정)
    /// ============================================================================
    /// 처음 이 파일을 세웠을 때 배포 에셋에는 직렬화 필드 <b>19개가 통째로 빠져 있었다</b>
    /// (등반 4박자 17개 + 진단 스위치 2개). 그 필드들은 역직렬화가 코드 기본값을 그대로 남기므로
    /// <b>코드가 이겼고</b>, 그래서 정상 동작했다 — 그게 이번 사고가 "새로 만든 건 다 멀쩡한데
    /// 숫자만 바꾼 옛 필드 셋만 죽었다"로 보인 이유다.
    ///
    /// <para>진짜 병은 "19개가 비어 있다"가 아니라 <b>"어떤 필드는 코드가 이기고 어떤 필드는 에셋이
    /// 이기는데, 어느 쪽인지 화면에 안 보인다"</b>였다. 경고만 달아 두면 규칙이 여전히 둘이고,
    /// 인스펙터 저장 한 번으로 조용히 뒤집힌다. 그래서 리더가 <b>19개를 전부 구웠다</b>
    /// (값은 그때의 코드 기본값 그대로 — 앱 동작은 한 톨도 바뀌지 않았다).</para>
    ///
    /// <para>이제 규칙은 하나다: <b>에셋이 언제나 이긴다. 다르면 대장에 등재돼야 한다.</b>
    /// 코드 기본값을 바꾸고 에셋을 안 고친 사람은 <b>즉시 빨간불</b>을 본다. 마찰이 느는 것은
    /// 부작용이 아니라 목적이다 — 그 마찰이 없어서 사고가 났다.</para>
    ///
    /// <para><b>결석은 이제 정상이 아니라 이상이다</b>:
    /// <see cref="모든_직렬화_필드가_에셋에_구워져_있다"/>가 실패로 잡는다. 신규 필드를 추가한
    /// 사람에게 "에셋에도 추가하라"고 문장으로 말해 준다.</para>
    ///
    /// <para>다만 <b>감사기 안의 판정 경계 코드는 그대로 둔다</b>(에셋에 키가 없으면 값 차이로 세지
    /// 않는다). 그것이 역직렬화의 실제 의미이고, 굽기가 빠진 필드를 <b>결석 1건</b>이 아니라
    /// <b>결석 1건 + 값 드리프트 1건</b>으로 두 번 세면 실패 메시지가 거짓말을 하기 때문이다.
    /// 실데이터에서는 더 이상 밟히지 않으므로
    /// <see cref="네거티브_컨트롤_에셋에_없는_필드는_값이_어긋나도_잡히지_않는다"/>가 그 경계를
    /// 합성 입력으로 계속 증명한다.</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 저장소에서 "항상 참인 단언"이 여러 번 나왔다)
    /// ============================================================================
    /// 대장이 지금 <b>비어 있으므로</b>(실측: 차이 0건) 본 검사만으로는 비교기가 죽어 있어도 초록이다.
    /// 그래서 감사기 <see cref="Audit"/>를 <b>순수 함수</b>로 떼어 두고, 합성 입력으로 여섯 갈래를
    /// 전부 실제로 빨갛게 만들어 본다. 그중 하나
    /// (<see cref="네거티브_컨트롤_2026_09_01_사고를_그대로_재현하면_정확히_세_건이_잡힌다"/>)는
    /// <b>사고 당시의 실제 값</b>으로 사고 그 자체를 재현한다.
    ///
    /// <para>또한 <see cref="감사기가_실제_에셋에서_충분한_필드를_읽었다"/>가 "빈 집합끼리 비교해서
    /// 초록"을 막고, 비교할 줄 모르는 타입의 필드가 새로 생기면 <b>조용히 통과하지 않고</b> 실패한다.</para>
    ///
    /// ============================================================================
    /// 플랫폼
    /// ============================================================================
    /// 이 파일은 플랫폼 중립이다 — <c>StickConfig</c>와 그 배포 에셋은 macOS/Windows/iPad/iPhone이
    /// 공유하는 단일 자산이고, 검사도 플랫폼 분기가 없다. macOS에서 잡히면 Windows에서도 잡힌다.
    ///
    /// <para>이 테스트는 <b>배포 에셋을 읽기만 한다</b>(절대 불변 원칙 3). 코드 기본값을 얻으려고
    /// 만드는 <see cref="ScriptableObject.CreateInstance{T}"/> 인스턴스는 메모리 전용이며 반드시
    /// 파기한다(<see cref="StickMate.Tests.PlayMode.LiveObjectGrowthGuardTests"/> 취지).</para>
    /// </summary>
    public sealed class ConfigAssetDriftLedgerTests
    {
        private const string LogPrefix = "[설정드리프트]";

        /// <summary>프리팹(Stickman.prefab)과 씬(Main.unity)이 배선해 쓰는 <b>바로 그</b> 배포 에셋.
        /// 이 경로가 유일한지는 <see cref="배포_설정_에셋은_프로젝트에_하나뿐이다"/>가 확인한다.</summary>
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>float 비교 허용 오차(상대). Unity의 YAML 왕복은 비트 보존이라 원래 0이어도 되지만,
        /// 1 ULP 잡음으로 스위트가 흔들리는 것을 막으려고 아주 작은 값을 둔다.
        /// <para>이 값이 실제 드리프트를 덮지 않는다는 근거: 이번 사고의 가장 작은 차이가
        /// 3.02 vs 3(상대 6.6e-3)로 여기서 <b>4자리 이상</b> 크다. 민감도는
        /// <see cref="네거티브_컨트롤_비교기는_1ULP_잡음은_넘기고_실제_차이는_잡는다"/>가 잠근다.</para></summary>
        private const float RelativeEpsilon = 1e-6f;

        /// <summary>"빈 집합끼리 비교해서 초록"을 막는 바닥선. 프로덕션 상수가 아니라 <b>건전성 하한</b>이라
        /// 숫자로 적는다(<see cref="ItemCatalogAssetParityTests"/>의 골든 길이 하한과 같은 성격).
        /// 실측 2026-09-01(굽기 후): 직렬화 필드 394개 / 에셋 키 394개(1:1).</summary>
        private const int SanityFloorFieldCount = 300;

        // ============================================================================
        // ★ 대장 — 에셋이 코드 기본값과 <b>의도적으로</b> 다른 필드
        // ============================================================================

        /// <summary>
        /// 에셋 값이 코드 기본값과 다른 필드는 <b>여기 한 줄</b>이 있어야 통과한다.
        /// 한 줄이 곧 하나의 승인 기록이다.
        ///
        /// <para><b>적는 법</b>:
        /// <c>new IntentionalDifference("필드명", 코드기본값핀, 에셋값핀, "왜 코드 기본값을 그대로 쓰지 않는가")</c>.
        /// 두 핀은 <b>등재 시점의 실측값</b>이다. 어느 한쪽이 나중에 움직이면 이 검사가 빨개져서
        /// "그 튜닝이 아직 유효한지 다시 판정하라"고 말한다 — 그것이 이번 사고를 막는 장치다.</para>
        ///
        /// <para><b>지우는 법</b>: 에셋을 코드 기본값으로 되돌렸으면 <b>여기서도 지운다</b>.
        /// 안 지우면 <see cref="대장_항목은_지금도_실제로_어긋나야_한다"/>가 빨개진다(대장이 스스로
        /// 낡지 않게 하는 장치다).</para>
        ///
        /// <para>★ 2026-09-01 실측: <b>등재할 차이가 0건</b>이다(리더가 <c>parkourClimbDuration</c>을
        /// 0.5 → 1.05로 고쳐 세 건이 전부 닫혔다). 대장이 비어 있다는 것은 "지금 배포 에셋은 코드
        /// 기본값과 한 값도 다르지 않다"는 뜻이지, 검사가 없다는 뜻이 아니다 — 위 네거티브 컨트롤
        /// 여섯 갈래가 감사기가 살아 있음을 매 실행 증명한다.</para>
        ///
        /// <para>대장을 비워 두는 것이 목적이 아니다. 다음에 <b>정당한 튜닝</b>이 생기면 여기 한 줄을
        /// 적어라. 그게 이 파일의 사용법이다.</para>
        /// </summary>
        private static readonly IntentionalDifference[] Ledger =
        {
            // (지금은 비어 있다 — 위 문단 참고)
        };

        // ============================================================================
        // 자료형
        // ============================================================================

        internal readonly struct IntentionalDifference
        {
            public readonly string Field;
            public readonly string CodeDefaultPin;
            public readonly string AssetValuePin;
            public readonly string Reason;

            public IntentionalDifference(string field, string codeDefaultPin, string assetValuePin, string reason)
            {
                Field = field;
                CodeDefaultPin = codeDefaultPin;
                AssetValuePin = assetValuePin;
                Reason = reason;
            }
        }

        /// <summary>한 필드의 세 사실: 코드 기본값 / 에셋이 역직렬화해 준 값 /
        /// <b>에셋 파일에 그 키가 물리적으로 적혀 있는가</b>. 셋째가 판정 경계다.</summary>
        internal readonly struct FieldPair
        {
            public readonly string Name;
            public readonly object CodeDefault;
            public readonly object AssetValue;
            public readonly bool PresentInAssetFile;

            public FieldPair(string name, object codeDefault, object assetValue, bool presentInAssetFile)
            {
                Name = name;
                CodeDefault = codeDefault;
                AssetValue = assetValue;
                PresentInAssetFile = presentInAssetFile;
            }
        }

        internal enum DriftKind
        {
            /// <summary>에셋이 코드 기본값과 다른데 대장에 없다 — 이번 사고의 형태.</summary>
            Unregistered,
            /// <summary>대장에 있는데 지금은 값이 같다 — 고쳐 놓고 대장에서 안 지웠다(자동 만료).</summary>
            LedgerNoLongerDiffers,
            /// <summary>★ 등재된 필드인데 <b>코드 기본값</b>이 핀에서 움직였다.</summary>
            LedgerCodeDefaultMoved,
            /// <summary>등재된 필드인데 <b>에셋 값</b>이 핀에서 움직였다.</summary>
            LedgerAssetValueMoved,
            /// <summary>대장이 존재하지 않는 필드를 가리킨다(이름이 바뀌었는데 대장만 남았다).</summary>
            LedgerUnknownField,
            /// <summary>대장이 에셋 파일에 없는 키를 가리킨다 — 대장이 거짓말을 하고 있다.</summary>
            LedgerFieldAbsentFromAsset,
            /// <summary>같은 필드가 대장에 두 번 등재됐다.</summary>
            LedgerDuplicate,
        }

        internal readonly struct Finding
        {
            public readonly DriftKind Kind;
            public readonly string Field;
            public readonly string Detail;

            public Finding(DriftKind kind, string field, string detail)
            {
                Kind = kind;
                Field = field;
                Detail = detail;
            }

            public override string ToString() => $"  · {Field}: {Detail}";
        }

        // ============================================================================
        // ★ 감사기 — 순수 함수. 실제 데이터와 네거티브 컨트롤이 <b>같은 함수</b>를 쓴다.
        // ============================================================================

        internal static List<Finding> Audit(IReadOnlyList<FieldPair> pairs, IReadOnlyList<IntentionalDifference> ledger)
        {
            var findings = new List<Finding>();

            var byName = new Dictionary<string, FieldPair>(pairs.Count);
            foreach (FieldPair p in pairs) byName[p.Name] = p;

            var registered = new HashSet<string>();

            foreach (IntentionalDifference entry in ledger)
            {
                if (!registered.Add(entry.Field))
                {
                    findings.Add(new Finding(DriftKind.LedgerDuplicate, entry.Field,
                        "대장에 같은 필드가 두 번 등재돼 있습니다 — 뒤의 한 줄이 앞의 한 줄을 조용히 덮습니다."));
                    continue;
                }

                if (!byName.TryGetValue(entry.Field, out FieldPair pair))
                {
                    findings.Add(new Finding(DriftKind.LedgerUnknownField, entry.Field,
                        $"StickConfig에 '{entry.Field}' 필드가 없습니다. 필드 이름이 바뀌었거나 삭제됐는데 " +
                        "대장 줄만 남았습니다 — 이 줄은 이제 아무것도 승인하지 않으면서 자리만 지킵니다."));
                    continue;
                }

                if (!pair.PresentInAssetFile)
                {
                    findings.Add(new Finding(DriftKind.LedgerFieldAbsentFromAsset, entry.Field,
                        $"에셋 파일에 '{entry.Field}' 키가 없습니다 — 이 필드는 코드 기본값이 그대로 " +
                        $"쓰이므로 '의도된 차이'가 존재할 수 없습니다. 대장에서 이 줄을 지우십시오. " +
                        $"(사유였던 것: {entry.Reason})"));
                    continue;
                }

                if (SameValue(pair.CodeDefault, pair.AssetValue))
                {
                    findings.Add(new Finding(DriftKind.LedgerNoLongerDiffers, entry.Field,
                        $"대장에 등재돼 있는데 지금은 코드 기본값과 에셋이 둘 다 {Fmt(pair.AssetValue)}로 같습니다. " +
                        "누군가 이미 맞췄습니다 — 대장에서 이 줄을 지우십시오. 남겨 두면 이 필드에서 " +
                        "일어나는 <b>다음</b> 드리프트를 조용히 덮습니다. " +
                        $"(사유였던 것: {entry.Reason})"));
                    continue;
                }

                if (!PinMatches(entry.CodeDefaultPin, pair.CodeDefault))
                {
                    findings.Add(new Finding(DriftKind.LedgerCodeDefaultMoved, entry.Field,
                        $"★ 코드 기본값이 움직였습니다: 대장에 박제된 핀 '{entry.CodeDefaultPin}' → 지금 " +
                        $"{Fmt(pair.CodeDefault)}. 에셋은 여전히 {Fmt(pair.AssetValue)}입니다.\n" +
                        $"       이 튜닝({entry.Reason})이 새 기본값 아래에서도 유효한지 <b>다시 판정</b>하십시오. " +
                        "유효하면 핀만 갱신하고, 아니면 에셋을 새 기본값으로 맞추고 이 줄을 지우십시오.\n" +
                        "       ※ 2026-09-01 사고가 정확히 이 형태였습니다(코드만 움직이고 에셋이 남았다)."));
                }

                if (!PinMatches(entry.AssetValuePin, pair.AssetValue))
                {
                    findings.Add(new Finding(DriftKind.LedgerAssetValueMoved, entry.Field,
                        $"에셋 값이 움직였습니다: 대장에 박제된 핀 '{entry.AssetValuePin}' → 지금 " +
                        $"{Fmt(pair.AssetValue)}. 튜닝을 새로 했다면 대장의 핀도 함께 갱신하십시오 " +
                        "(대장은 승인 기록이므로, 승인한 값과 실제 값이 달라지면 기록이 무의미해집니다)."));
                }
            }

            foreach (FieldPair pair in pairs)
            {
                // ★ 판정 경계 — 에셋 파일에 키가 없으면 코드 기본값이 이긴다 = 정상. 차이로 세지 않는다.
                if (!pair.PresentInAssetFile) continue;
                if (registered.Contains(pair.Name)) continue;
                if (SameValue(pair.CodeDefault, pair.AssetValue)) continue;

                findings.Add(new Finding(DriftKind.Unregistered, pair.Name,
                    $"코드 기본값 {Fmt(pair.CodeDefault)} ≠ 에셋 값 {Fmt(pair.AssetValue)} " +
                    "(실제로 동작하는 것은 <b>에셋 값</b>입니다)"));
            }

            return findings;
        }

        // ============================================================================
        // 값 비교 / 표기 / 핀 해석
        // ============================================================================

        internal static bool SameValue(object a, object b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            if (a.GetType() != b.GetType()) return false;

            switch (a)
            {
                case float fa: return NearlyEqual(fa, (float)b);
                case Color ca:
                {
                    var cb = (Color)b;
                    return NearlyEqual(ca.r, cb.r) && NearlyEqual(ca.g, cb.g)
                        && NearlyEqual(ca.b, cb.b) && NearlyEqual(ca.a, cb.a);
                }
                case Vector2 v2a:
                {
                    var v2b = (Vector2)b;
                    return NearlyEqual(v2a.x, v2b.x) && NearlyEqual(v2a.y, v2b.y);
                }
                case Vector3 v3a:
                {
                    var v3b = (Vector3)b;
                    return NearlyEqual(v3a.x, v3b.x) && NearlyEqual(v3a.y, v3b.y) && NearlyEqual(v3a.z, v3b.z);
                }
                default: return a.Equals(b);
            }
        }

        private static bool NearlyEqual(float a, float b)
        {
            if (a == b) return true;
            float scale = Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
            return Mathf.Abs(a - b) <= RelativeEpsilon * scale;
        }

        internal static string Fmt(object v)
        {
            switch (v)
            {
                case null: return "(null)";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case Color c:
                    return $"RGBA({c.r.ToString("R", CultureInfo.InvariantCulture)}, " +
                           $"{c.g.ToString("R", CultureInfo.InvariantCulture)}, " +
                           $"{c.b.ToString("R", CultureInfo.InvariantCulture)}, " +
                           $"{c.a.ToString("R", CultureInfo.InvariantCulture)})";
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>대장의 핀 문자열이 지금 값과 같은가. 숫자는 <b>파싱해서</b> 비교하므로
        /// "0.5" / "0.50" / "0.5f" 를 손으로 적어도 같은 뜻이 된다(핀 갱신 마찰을 줄인다).</summary>
        internal static bool PinMatches(string pin, object actual)
        {
            if (pin == null) return false;
            string trimmed = pin.Trim().TrimEnd('f', 'F');

            switch (actual)
            {
                case float f:
                    return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float pf)
                        && NearlyEqual(pf, f);
                case int i:
                    return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pi)
                        && pi == i;
                case bool b:
                    return bool.TryParse(pin.Trim(), out bool pb) && pb == b;
                default:
                    // enum / Color / string 등은 표기 문자열로 비교한다.
                    return string.Equals(pin.Trim(), Fmt(actual), StringComparison.Ordinal);
            }
        }

        // ============================================================================
        // 실제 데이터 수집 — 리플렉션(코드 기본값) + 역직렬화(에셋 값) + YAML 키 스캔(물리적 존재)
        // ============================================================================

        private static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config,
                $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}\n" +
                "경로가 바뀌었다면 이 상수를 함께 고치십시오 — 그 전까지 이 감사는 아무것도 지키지 않습니다.");
            return config;
        }

        /// <summary>StickConfig가 <b>실제로 직렬화하는</b> 필드들. 프로퍼티/상수/[NonSerialized]는 제외된다.</summary>
        private static List<FieldInfo> SerializedFields()
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo f in typeof(StickConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.DeclaringType != typeof(StickConfig)) continue;
                if (Attribute.IsDefined(f, typeof(NonSerializedAttribute))) continue;
                fields.Add(f);
            }
            return fields;
        }

        /// <summary>에셋 <b>파일</b>에 물리적으로 적혀 있는 최상위 키 이름들(YAML 2칸 들여쓰기).
        /// Unity 내부 키(<c>m_*</c>)와 <c>serializedVersion</c>은 제외한다.</summary>
        private static HashSet<string> ReadAssetFileKeys()
        {
            string path = Path.Combine(RepoRoot, DeployedConfigPath);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 에셋 파일을 찾지 못했습니다: {path}");

            var keys = new HashSet<string>();
            foreach (Match m in Regex.Matches(File.ReadAllText(path), @"^  ([A-Za-z_][A-Za-z0-9_]*):",
                                              RegexOptions.Multiline))
            {
                string key = m.Groups[1].Value;
                if (key.StartsWith("m_", StringComparison.Ordinal)) continue;
                if (key == "serializedVersion") continue;
                keys.Add(key);
            }
            return keys;
        }

        private sealed class Snapshot
        {
            public List<FieldPair> Pairs;
            public List<string> AbsentFromAssetFile;
            public List<string> GhostKeys;
            public int AssetFileKeyCount;
        }

        private static Snapshot Collect()
        {
            StickConfig deployed = LoadDeployedConfig();
            HashSet<string> fileKeys = ReadAssetFileKeys();
            List<FieldInfo> fields = SerializedFields();

            var snap = new Snapshot
            {
                Pairs = new List<FieldPair>(fields.Count),
                AbsentFromAssetFile = new List<string>(),
                GhostKeys = new List<string>(),
                AssetFileKeyCount = fileKeys.Count,
            };

            // 코드 기본값 = 필드 초기자만 적용된 새 인스턴스. 메모리 전용이며 반드시 파기한다.
            var codeDefaults = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                foreach (FieldInfo f in fields)
                {
                    AssertComparableType(f);

                    bool present = fileKeys.Contains(f.Name);
                    if (!present) snap.AbsentFromAssetFile.Add(f.Name);

                    snap.Pairs.Add(new FieldPair(f.Name, f.GetValue(codeDefaults), f.GetValue(deployed), present));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(codeDefaults);
            }

            var known = new HashSet<string>();
            foreach (FieldInfo f in fields) known.Add(f.Name);
            foreach (string key in fileKeys)
            {
                if (!known.Contains(key)) snap.GhostKeys.Add(key);
            }

            snap.AbsentFromAssetFile.Sort(StringComparer.Ordinal);
            snap.GhostKeys.Sort(StringComparer.Ordinal);
            return snap;
        }

        /// <summary>비교할 줄 모르는 타입이 새로 생기면 <b>조용히 통과하지 않는다</b>.
        /// 배열/리스트/중첩 구조체가 들어오면 여기서 멈추고 감사기를 확장하라고 말한다 —
        /// 그러지 않으면 <c>object.Equals</c>가 참조 비교로 떨어져 "항상 다르다"거나
        /// "항상 같다"는 거짓 결과를 낸다.</summary>
        private static void AssertComparableType(FieldInfo f)
        {
            Type t = f.FieldType;
            bool ok = t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(string)
                   || t == typeof(Color) || t == typeof(Vector2) || t == typeof(Vector3) || t.IsEnum;

            Assert.IsTrue(ok,
                $"{LogPrefix} StickConfig.{f.Name}의 타입 {t.Name}은(는) 이 감사기가 비교할 줄 모릅니다.\n" +
                "SameValue/Fmt/PinMatches에 그 타입을 추가하십시오. 추가하기 전까지 이 필드의 드리프트는 " +
                "검사되지 않으며, 그 상태로 초록불이 뜨는 것이 이 프로젝트가 반복해 온 실패입니다.");
        }

        private static string Render(IEnumerable<Finding> findings)
        {
            var sb = new StringBuilder();
            foreach (Finding f in findings) sb.Append(f).Append('\n');
            return sb.ToString();
        }

        private static List<Finding> Of(IEnumerable<Finding> findings, DriftKind kind)
        {
            var list = new List<Finding>();
            foreach (Finding f in findings)
            {
                if (f.Kind == kind) list.Add(f);
            }
            return list;
        }

        /// <summary>★ 요구사항 1 — 실패 메시지는 <b>다음 사람에게 무엇을 하라고</b> 말해야 한다.</summary>
        private const string TwoBranches =
            "\n무엇을 하면 되는가 — 두 갈래 중 하나를 고르십시오(둘 다 필요할 수도 있습니다).\n" +
            "  (가) 코드 기본값이 새 의도라면 → <b>에셋을 고치십시오</b>.\n" +
            "       " + DeployedConfigPath + " 의 해당 줄을 코드 기본값과 같게 바꿉니다.\n" +
            "       ScriptableObject는 직렬화된 값이 필드 초기자를 이깁니다 — 에셋을 안 고치면\n" +
            "       코드 기본값 변경은 <b>사용자 화면에 존재하지 않습니다</b>.\n" +
            "  (나) 에셋 값이 의도된 튜닝이라면 → <b>대장에 등재하십시오</b>.\n" +
            "       ConfigAssetDriftLedgerTests.Ledger 에 한 줄:\n" +
            "         new IntentionalDifference(\"필드명\", \"코드기본값\", \"에셋값\", \"왜 코드 기본값을 안 쓰는가\")\n" +
            "       두 값을 함께 박제하므로, 나중에 어느 한쪽이 움직이면 이 검사가 다시 물어봅니다.";

        // ============================================================================
        // 1. 본 검사
        // ============================================================================

        [Test]
        public void 에셋이_코드_기본값과_다른_필드는_전부_대장에_등재돼_있다()
        {
            Snapshot snap = Collect();
            List<Finding> unregistered = Of(Audit(snap.Pairs, Ledger), DriftKind.Unregistered);

            Debug.Log($"{LogPrefix} 직렬화 필드 {snap.Pairs.Count}개 / 에셋 파일 키 {snap.AssetFileKeyCount}개 / " +
                      $"결석 {snap.AbsentFromAssetFile.Count}개 / 유령 {snap.GhostKeys.Count}개 / " +
                      $"대장 {Ledger.Length}줄 → 등재되지 않은 차이 {unregistered.Count}건.");

            Assert.IsEmpty(unregistered,
                $"{LogPrefix} 배포 에셋이 코드 기본값과 다른데 대장에 없는 필드가 " +
                $"{unregistered.Count}건입니다.\n" + Render(unregistered) + TwoBranches);
        }

        [Test]
        public void 대장_항목은_지금도_실제로_어긋나야_한다()
        {
            // 자동 만료 — 고쳐 놓고 대장에서 안 지우면, 그 줄이 이 필드의 <b>다음</b> 드리프트를 덮는다.
            List<Finding> stale = Of(Audit(Collect().Pairs, Ledger), DriftKind.LedgerNoLongerDiffers);

            Assert.IsEmpty(stale,
                $"{LogPrefix} 대장에 등재돼 있지만 지금은 차이가 아닌 항목이 {stale.Count}건입니다 " +
                "— 대장이 낡았습니다.\n" + Render(stale) +
                "\n대장에서 그 줄을 지우십시오. 대장은 '지금 살아 있는 차이'만 담습니다.");
        }

        [Test]
        public void 대장_항목의_핀이_지금_값과_일치한다()
        {
            List<Finding> all = Audit(Collect().Pairs, Ledger);
            var moved = new List<Finding>();
            moved.AddRange(Of(all, DriftKind.LedgerCodeDefaultMoved));
            moved.AddRange(Of(all, DriftKind.LedgerAssetValueMoved));

            Assert.IsEmpty(moved,
                $"{LogPrefix} 대장에 박제된 값과 지금 값이 다른 항목이 {moved.Count}건입니다.\n" + Render(moved) +
                TwoBranches);
        }

        [Test]
        public void 대장이_존재하지_않는_필드나_키를_가리키지_않는다()
        {
            List<Finding> all = Audit(Collect().Pairs, Ledger);
            var broken = new List<Finding>();
            broken.AddRange(Of(all, DriftKind.LedgerUnknownField));
            broken.AddRange(Of(all, DriftKind.LedgerFieldAbsentFromAsset));
            broken.AddRange(Of(all, DriftKind.LedgerDuplicate));

            Assert.IsEmpty(broken,
                $"{LogPrefix} 대장 줄 {broken.Count}건이 실재하지 않는 대상을 가리킵니다.\n" + Render(broken) +
                "\n이름이 바뀐 필드를 대장이 계속 가리키면, 새 이름의 필드는 <b>아무 승인 없이</b> " +
                "검사에 들어오고 옛 줄은 영원히 초록입니다 — 커버리지 구멍이 열리는 가장 흔한 길입니다.");
        }

        // ============================================================================
        // 2. ★ 결석 = 이상 — 굽기가 끝났으므로 규칙이 하나다
        // ============================================================================

        [Test]
        public void 모든_직렬화_필드가_에셋에_구워져_있다()
        {
            Snapshot snap = Collect();

            // 굽기 전에는 이 검사가 <b>경고 로그</b>였다. 리더 정책 판정(2026-09-01)으로 19개를 전부
            // 구운 뒤 성격이 바뀌었다 — 이제 결석은 "코드 기본값이 이기는 정상 상태"가 아니라
            // "규칙이 둘로 갈라진 이상 상태"다.
            Assert.IsEmpty(snap.AbsentFromAssetFile,
                $"{LogPrefix} StickConfig의 직렬화 필드 {snap.AbsentFromAssetFile.Count}개가 배포 에셋에 " +
                $"없습니다: {string.Join(", ", snap.AbsentFromAssetFile)}\n" +
                "\n왜 실패인가.\n" +
                "  에셋에 키가 없는 필드는 <b>코드 기본값이 이깁니다</b>. 지금 당장은 잘 돕니다 — 그게 함정입니다.\n" +
                "  누가 인스펙터에서 이 에셋을 열고 Ctrl+S만 눌러도 Unity가 그 필드들을 현재 값으로 " +
                "박제하고,\n  그 순간부터 <b>코드 기본값을 바꿔도 화면이 안 바뀝니다</b> " +
                "(2026-09-01 사고의 기전 그 자체입니다).\n" +
                "  그래서 이 저장소의 규칙은 하나입니다: <b>에셋이 언제나 이긴다. 다르면 대장에 등재한다.</b>\n" +
                "\n무엇을 하면 되는가 — 필드를 새로 추가했다면 <b>에셋에도 추가하십시오</b>.\n" +
                "  (가) 가장 쉬운 길: Unity 인스펙터에서 " + DeployedConfigPath + " 를 열고 저장하면\n" +
                "       Unity가 빠진 필드를 전부 현재 코드 기본값으로 써 줍니다.\n" +
                "  (나) 손으로 적는다면: 그 필드의 <b>코드 기본값과 정확히 같은 값</b>을 적으십시오.\n" +
                "       한 비트라도 어긋나면 이 파일의 대장 검사가 곧바로 '등재되지 않은 차이'로 잡습니다\n" +
                "       (float은 왕복 가능한 최단 십진 표기로 — 예: 0.10f는 0.1, 40f는 40).\n" +
                "  ※ 값을 <b>일부러</b> 다르게 적고 싶다면 그것이 바로 '의도된 튜닝'이므로 Ledger에 등재하십시오.");

            // 결석이 0이므로 아래 루프는 비어 있다. 그래도 남겨 둔다 — 굽기가 되돌려지는 날
            // "역직렬화가 코드 기본값을 남긴다"는 전제가 여전히 참인지 함께 확인해 준다.
            foreach (FieldPair pair in snap.Pairs)
            {
                if (pair.PresentInAssetFile) continue;
                Assert.IsTrue(SameValue(pair.CodeDefault, pair.AssetValue),
                    $"{LogPrefix} '{pair.Name}'은 에셋 파일에 키가 없는데 역직렬화 값" +
                    $"({Fmt(pair.AssetValue)})이 코드 기본값({Fmt(pair.CodeDefault)})과 다릅니다. " +
                    "이 전제가 깨지면 감사기의 판정 경계 자체가 틀린 것이므로 재설계해야 합니다.");
            }

            Debug.Log($"{LogPrefix} 직렬화 필드 {snap.Pairs.Count}개가 전부 에셋에 구워져 있습니다 " +
                      "— 규칙은 하나입니다: 에셋이 언제나 이깁니다.");
        }

        [Test]
        public void 에셋_키와_직렬화_필드가_정확히_일대일이다()
        {
            // 위 두 검사(결석 0 / 유령 0)의 결론을 한 줄로 다시 못 박는다. 어느 쪽이든 새는 순간
            // "코드가 이기는 필드"와 "에셋이 이기는 필드"가 섞여 규칙이 다시 둘이 된다.
            Snapshot snap = Collect();
            Assert.AreEqual(snap.Pairs.Count, snap.AssetFileKeyCount,
                $"{LogPrefix} 직렬화 필드 {snap.Pairs.Count}개 vs 에셋 키 {snap.AssetFileKeyCount}개 — " +
                $"결석 {snap.AbsentFromAssetFile.Count}개 / 유령 {snap.GhostKeys.Count}개.");
        }

        [Test]
        public void 에셋에_코드에_없는_유령_키가_남아있지_않다()
        {
            Snapshot snap = Collect();

            Assert.IsEmpty(snap.GhostKeys,
                $"{LogPrefix} 에셋 파일에 StickConfig가 더는 갖고 있지 않은 키가 " +
                $"{snap.GhostKeys.Count}개 남아 있습니다: {string.Join(", ", snap.GhostKeys)}\n" +
                "필드 이름을 바꾸거나 지웠다는 뜻이고, 그 순간 <b>그 필드에 걸려 있던 튜닝이 조용히 " +
                "코드 기본값으로 되돌아갑니다</b>(이름을 바꾼 사람은 그 사실을 모릅니다).\n" +
                "  (가) 이름을 바꿨다면 → 에셋의 키도 새 이름으로 바꾸거나, [FormerlySerializedAs]를 답니다.\n" +
                "  (나) 정말 지운 필드라면 → 에셋에서 그 줄을 지워 파일을 정리합니다.");
        }

        // ============================================================================
        // 3. 감사기 자체가 살아 있는가 (빈 집합 비교로 초록이 뜨는 것을 막는다)
        // ============================================================================

        [Test]
        public void 감사기가_실제_에셋에서_충분한_필드를_읽었다()
        {
            Snapshot snap = Collect();

            Assert.Greater(snap.Pairs.Count, SanityFloorFieldCount,
                $"{LogPrefix} 리플렉션이 StickConfig 직렬화 필드를 {snap.Pairs.Count}개밖에 못 찾았습니다 " +
                $"(하한 {SanityFloorFieldCount}). 필드 수집이 깨지면 이 감사는 <b>빈 집합끼리 비교해서</b> " +
                "영원히 초록입니다.");

            Assert.Greater(snap.AssetFileKeyCount, SanityFloorFieldCount,
                $"{LogPrefix} 에셋 YAML에서 키를 {snap.AssetFileKeyCount}개밖에 못 읽었습니다 " +
                $"(하한 {SanityFloorFieldCount}). 파서가 깨지면 <b>모든 필드가 '결석'으로 분류되어</b> " +
                "판정 경계가 전 필드를 삼키고, 이 감사는 아무것도 검사하지 않게 됩니다 " +
                "— 이번 사고와 정확히 같은 침묵입니다.");

            int compared = 0;
            foreach (FieldPair p in snap.Pairs)
            {
                if (p.PresentInAssetFile) compared++;
            }
            Assert.Greater(compared, SanityFloorFieldCount,
                $"{LogPrefix} 실제로 비교된 필드가 {compared}개뿐입니다(하한 {SanityFloorFieldCount}).");

            Debug.Log($"{LogPrefix} 감사 범위 확인 — 비교된 필드 {compared}개 / 결석 {snap.AbsentFromAssetFile.Count}개.");
        }

        [Test]
        public void 배포_설정_에셋은_프로젝트에_하나뿐이다()
        {
            string[] guids = AssetDatabase.FindAssets("t:StickConfig");
            var paths = new List<string>();
            foreach (string g in guids) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            paths.Sort(StringComparer.Ordinal);

            Assert.AreEqual(1, paths.Count,
                $"{LogPrefix} StickConfig 에셋이 {paths.Count}개입니다: {string.Join(", ", paths)}\n" +
                "이 감사는 배포 에셋 하나만 봅니다 — 둘째 에셋이 생기면 그쪽 드리프트는 검사되지 않고, " +
                "프리팹/씬이 어느 쪽을 배선했는지에 따라 실제 동작이 갈립니다.");

            Assert.AreEqual(DeployedConfigPath, paths[0],
                $"{LogPrefix} 배포 설정 에셋이 옮겨졌습니다: {paths[0]}. DeployedConfigPath 상수를 함께 " +
                "고치십시오(그 전까지 이 감사는 없는 파일을 보게 됩니다).");
        }

        // ============================================================================
        // 4. ★ 네거티브 컨트롤 — 감사기가 <b>실제로</b> 빨개지는가
        //    (대장이 비어 있으므로, 이것들이 없으면 위 검사는 전부 "항상 참인 단언"이다)
        // ============================================================================

        private static FieldPair P(string name, object code, object asset, bool present = true)
            => new FieldPair(name, code, asset, present);

        [Test]
        public void 네거티브_컨트롤_등재되지_않은_차이는_실제로_잡힌다()
        {
            var pairs = new[] { P("someTuning", 1.05f, 0.5f), P("untouched", 3f, 3f) };
            List<Finding> findings = Audit(pairs, new IntentionalDifference[0]);

            Assert.AreEqual(1, findings.Count, "차이 1건만 잡혀야 합니다.");
            Assert.AreEqual(DriftKind.Unregistered, findings[0].Kind);
            Assert.AreEqual("someTuning", findings[0].Field);
            StringAssert.Contains("1.05", findings[0].Detail);
            StringAssert.Contains("0.5", findings[0].Detail);
        }

        [Test]
        public void 네거티브_컨트롤_대장에_등재하면_통과한다()
        {
            var pairs = new[] { P("someTuning", 1.05f, 0.5f) };
            var ledger = new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "합성 컨트롤 — 정당한 튜닝을 막지 않는다"),
            };

            Assert.IsEmpty(Audit(pairs, ledger),
                "대장에 등재된 차이는 통과해야 합니다 — 그러지 않으면 이 장치가 정당한 튜닝을 전부 막습니다.");
        }

        [Test]
        public void 네거티브_컨트롤_등재됐어도_코드_기본값이_움직이면_잡힌다()
        {
            // ★ 2026-09-01 사고의 형태 그 자체 — "이미 등재된 필드"라는 이유로 조용히 덮이면 안 된다.
            var pairs = new[] { P("someTuning", 2.0f, 0.5f) };   // 코드 기본값이 1.05 -> 2.0으로 움직였다
            var ledger = new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "합성 컨트롤"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(DriftKind.LedgerCodeDefaultMoved, findings[0].Kind);
            StringAssert.Contains("다시 판정", findings[0].Detail);
        }

        [Test]
        public void 네거티브_컨트롤_등재됐어도_에셋_값이_움직이면_잡힌다()
        {
            var pairs = new[] { P("someTuning", 1.05f, 0.8f) };  // 에셋이 0.5 -> 0.8로 움직였다
            var ledger = new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "합성 컨트롤"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(DriftKind.LedgerAssetValueMoved, findings[0].Kind);
        }

        [Test]
        public void 네거티브_컨트롤_고쳐진_항목이_대장에_남아_있으면_잡힌다()
        {
            var pairs = new[] { P("someTuning", 1.05f, 1.05f) };  // 에셋을 맞췄다
            var ledger = new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "합성 컨트롤"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(DriftKind.LedgerNoLongerDiffers, findings[0].Kind);
        }

        [Test]
        public void 네거티브_컨트롤_대장이_사라진_필드를_가리키면_잡힌다()
        {
            var pairs = new[] { P("renamedTuning", 1.05f, 0.5f) };
            var ledger = new[]
            {
                new IntentionalDifference("oldTuningName", "1.05", "0.5", "합성 컨트롤"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(2, findings.Count,
                "이름이 바뀌면 (a) 옛 줄이 죽었다 + (b) 새 이름이 승인 없이 어긋난다 — 두 건 다 나와야 합니다.");
            CollectionAssert.AreEquivalent(
                new[] { DriftKind.LedgerUnknownField, DriftKind.Unregistered },
                new[] { findings[0].Kind, findings[1].Kind });
        }

        [Test]
        public void 네거티브_컨트롤_같은_필드를_두_번_등재하면_잡힌다()
        {
            var pairs = new[] { P("someTuning", 1.05f, 0.5f) };
            var ledger = new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "첫 줄"),
                new IntentionalDifference("someTuning", "9.9", "9.9", "둘째 줄이 첫 줄을 덮는다"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(DriftKind.LedgerDuplicate, findings[0].Kind);
        }

        // ---- 판정 경계 ----

        [Test]
        public void 네거티브_컨트롤_에셋에_없는_필드는_값이_어긋나도_잡히지_않는다()
        {
            // ★ 이번 사고의 판정 경계. 파일에 키가 없으면 코드 기본값이 이긴다 = 정상 동작이다.
            //   (실제 역직렬화에서는 두 값이 같아지지만, 경계가 <b>구현되어 있다</b>는 사실 자체를
            //    일부러 어긋난 합성 입력으로 증명한다 — 우연히 같아서 통과하는 것이 아님을 보인다.)
            var pairs = new[] { P("newFieldNotInAsset", 1.05f, 0.5f, present: false) };

            Assert.IsEmpty(Audit(pairs, new IntentionalDifference[0]),
                "에셋에 없는 필드를 차이로 세면, 신규 필드를 추가할 때마다 이 검사가 빨개져서 " +
                "아무도 안 보게 됩니다(그러면 진짜 드리프트도 함께 묻힙니다).");

            // 같은 입력에 present=true만 켜면 반드시 잡혀야 한다 — 경계가 '항상 통과'가 아님을 증명한다.
            Assert.AreEqual(1, Audit(new[] { P("newFieldNotInAsset", 1.05f, 0.5f) },
                                     new IntentionalDifference[0]).Count,
                "present 플래그를 켰는데도 안 잡힌다면, 이 경계는 경계가 아니라 전면 면제입니다.");
        }

        [Test]
        public void 네거티브_컨트롤_에셋에_없는_필드를_대장에_등재하면_잡힌다()
        {
            var pairs = new[] { P("newFieldNotInAsset", 1.05f, 1.05f, present: false) };
            var ledger = new[]
            {
                new IntentionalDifference("newFieldNotInAsset", "1.05", "0.5", "존재한 적 없는 차이"),
            };

            List<Finding> findings = Audit(pairs, ledger);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual(DriftKind.LedgerFieldAbsentFromAsset, findings[0].Kind);
        }

        // ---- 비교기 민감도 ----

        [Test]
        public void 네거티브_컨트롤_비교기는_1ULP_잡음은_넘기고_실제_차이는_잡는다()
        {
            Assert.IsTrue(SameValue(1.05f, 1.05f));
            // 실제로 <b>다른 비트</b>인데 허용 오차 안인 값 — "같은 값을 두 번 넣어서 통과"가 아님을 보인다.
            // 허용 오차의 절반 — 1.05 근방의 float 간격(약 1.2e-7)보다 크므로 <b>정말로 다른 비트</b>가 된다.
            float noise = RelativeEpsilon * 0.5f;
            Assert.AreNotEqual(1.05f, 1.05f + noise, "합성 잡음이 반올림으로 사라졌습니다 — 이 단언이 아무것도 증명하지 못합니다.");
            Assert.IsTrue(SameValue(1.05f, 1.05f + noise), "허용 오차 안의 잡음으로 스위트가 흔들리면 안 됩니다.");

            // 이번 사고의 <b>가장 작은</b> 차이(3.02 vs 3)가 반드시 잡혀야 한다.
            Assert.IsFalse(SameValue(3.02f, 3f), "가장 작은 실제 드리프트를 허용 오차가 삼키고 있습니다.");
            Assert.IsFalse(SameValue(1.05f, 0.5f));
            Assert.IsFalse(SameValue(0f, 0.7f));

            Assert.IsTrue(SameValue(true, true));
            Assert.IsFalse(SameValue(true, false));
            Assert.IsTrue(SameValue(Color.black, new Color(0f, 0f, 0f, 1f)));
            Assert.IsFalse(SameValue(Color.black, Color.white));
            Assert.IsTrue(SameValue(StickmanInkColor.Black, StickmanInkColor.Black));
            Assert.IsFalse(SameValue(StickmanInkColor.Black, StickmanInkColor.White));

            // 핀 해석 — 표기 흔들림은 흡수하되 값이 다르면 잡는다.
            Assert.IsTrue(PinMatches("0.50", 0.5f));
            Assert.IsTrue(PinMatches("0.5f", 0.5f));
            Assert.IsFalse(PinMatches("0.5", 0.51f));
            Assert.IsFalse(PinMatches("아무거나", 0.5f));
            Assert.IsTrue(PinMatches("true", true));
            Assert.IsTrue(PinMatches("Black", StickmanInkColor.Black));
            Assert.IsFalse(PinMatches("Black", StickmanInkColor.White));
        }

        // ---- ★ 사고 재현 ----

        [Test]
        public void 네거티브_컨트롤_2026_09_01_사고를_그대로_재현하면_정확히_세_건이_잡힌다()
        {
            // ★ 여기 적힌 숫자는 <b>사고 당시의 역사적 실측값</b>이지 지금의 프로덕션 상수가 아니다.
            //   그래서 코드 기본값이 앞으로 어떻게 바뀌어도 이 컨트롤은 갱신하지 않는다
            //   (AccessoryFallbackIconParityTests의 "컨트롤_옛_…" 관례와 같다).
            //   지금 값을 리플렉션으로 읽어 오면, 그 값이 바뀌는 날 이 재현이 조용히 무의미해진다.
            var pairs = new List<FieldPair>
            {
                // 값이 어긋났던 옛 필드 3개 — 전부 잡혀야 한다.
                P("parkourClimbDuration",        1.05f, 0.5f),
                P("dialogueMinVisibleSeconds",   0f,    0.7f),
                P("landingCrouchDeepFallHeights", 3.02f, 3f),

                // 그날 새로 생겨 <b>에셋에 아예 없던</b> 필드들 — 코드 기본값이 살아남아 정상 동작했다.
                // 하나도 잡히면 안 된다. 이것이 "새 기능은 다 되는데 옛 필드 셋만 죽었다"의 정체다.
                P("parkourClimbReachFraction",   0.18f, 0.18f, present: false),
                P("parkourClimbHangFraction",    0.34f, 0.34f, present: false),
                P("parkourClimbPullFraction",    0.74f, 0.74f, present: false),
                P("parkourClimbReleaseFraction", 0.88f, 0.88f, present: false),
                P("parkourClimbMantleArmDegrees", 62f,  62f,   present: false),
            };

            List<Finding> findings = Audit(pairs, new IntentionalDifference[0]);

            Assert.AreEqual(3, findings.Count,
                "사고 당시 상태를 넣었는데 3건이 안 나옵니다 — 감사기가 사고를 재현하지 못하면 " +
                "재발도 못 잡습니다.\n" + Render(findings));

            var caught = new List<string>();
            foreach (Finding f in findings)
            {
                Assert.AreEqual(DriftKind.Unregistered, f.Kind);
                caught.Add(f.Field);
            }
            CollectionAssert.AreEquivalent(
                new[] { "parkourClimbDuration", "dialogueMinVisibleSeconds", "landingCrouchDeepFallHeights" },
                caught);

            Debug.Log($"{LogPrefix} 사고 재현 확인 — 값이 어긋난 3건은 전부 잡히고, 에셋에 없던 신규 필드 " +
                      "5건은 하나도 잡히지 않았습니다(판정 경계가 정확히 그 자리에 있습니다).");
        }

        [Test]
        public void 네거티브_컨트롤_감사기가_대장을_실제로_참조한다()
        {
            // Audit이 ledger 인자를 무시하고 항상 빈 목록을 뱉어도 본 검사는 초록이다(대장이 비어 있으므로).
            // 같은 입력을 대장 유무만 바꿔 두 번 돌려, 결과가 <b>달라지는지</b>를 직접 본다.
            var pairs = new[] { P("someTuning", 1.05f, 0.5f) };

            int without = Audit(pairs, new IntentionalDifference[0]).Count;
            int with = Audit(pairs, new[]
            {
                new IntentionalDifference("someTuning", "1.05", "0.5", "합성 컨트롤"),
            }).Count;

            Assert.AreEqual(1, without);
            Assert.AreEqual(0, with);
        }
    }
}
