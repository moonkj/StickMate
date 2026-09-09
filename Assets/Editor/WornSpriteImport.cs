using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// ★★ <b>착용 비트맵 임포트 규격 + 알파 타이트 박스 굽기</b>(2026-09-09).
    /// 설계 정본: <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §19-8-3 / §19-8-4 / §19-8-5.
    ///
    /// ============================================================================
    /// ★ 어제(<c>PackCardIconImport</c>)와 <b>일부러 다르게</b> 한 것 두 가지
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>메뉴가 아니라 <see cref="AssetPostprocessor"/>가 규격을 건다.</b>
    ///     어제 라운드에서 정확히 이 부류의 결함이 났다 — <c>platformSettings</c>가
    ///     <c>.meta</c>에 <b>적혀 있는데 <c>overridden: 0</c>이라 적용되지 않았다</b>.
    ///     사람이 메뉴를 눌러야 규격이 걸리는 구조에서는 «누르는 것을 잊는» 경로가 영원히 남는다.
    ///     후처리기는 <b>파일이 폴더에 놓이는 순간</b> 도므로 그 경로가 구조적으로 없다.</item>
    ///   <item><b>플랫폼 오버라이드를 <c>overridden: 1</c>로 켠다.</b> 어제는 끄는 쪽이 옳았다
    ///     (그라데이션 카드 그림이라 두 플랫폼이 <b>같은 비트</b>여야 했다). 이번은 반대다 —
    ///     몸에 붙는 그림은 42종 × 최대 2장이 <b>상주</b>하므로(§19-8-4) 플랫폼마다 자기 블록
    ///     압축을 써야 한다: Standalone <c>BC7</c> / iOS <c>ASTC 6×6</c>.
    ///     256²에서 무압축 RGBA32는 63장 <b>84MB</b>, 블록 압축은 <b>5.3MB</b>다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 값의 근거 (숫자를 지어내지 않았다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>maxTextureSize 256</b> — 가장 크게 쓰이는 소비처가 카드 72pt(Retina ×2 = 144px,
    ///     ×3 폰 = 216px)다. 256이면 모든 표면에서 오버샘플이고, 몸(19~54px)에서는 5~13배다.
    ///     512는 가장 큰 소비처에서도 2.4배 낭비, 128은 카드(216px)에서 부족하다(§19-8-4).</item>
    ///   <item><b>Sprite / Single / Center 피벗</b> — 렌더러가 캔버스 중심을 앵커에 맞추고
    ///     <c>flipX</c>를 피벗 축으로 뒤집는다. 피벗이 중심이 아니면 좌우 반전이 자리를 옮긴다.</item>
    ///   <item><b>Tight 메시</b> — 투명 여백의 오버드로를 줄인다. <b>그러나 이 프로젝트는
    ///     그 <c>bounds</c>를 믿지 않는다</b>(§19-12-2: Tight가 <c>Sprite.bounds</c>를 실제로
    ///     줄이는지는 Unity 버전에 따라 다르고 실기로 확인되지 않았다). 배치와 잉크 범위는
    ///     <c>rect</c>와 <b>구운 알파 박스</b>에서만 나온다.</item>
    ///   <item><b>밉맵 켬</b> — 몸에서 256 → 19~54px, 즉 5~13배 축소다. 밉맵이 없으면 그 배율에서
    ///     언더샘플링이 나고 증상은 «선이 끊겨 보인다»로 나타난다.</item>
    ///   <item><b>alphaIsTransparency</b> — 꺼져 있으면 축소·밉맵 과정에서 투명 픽셀의 RGB(보통 검정)가
    ///     섞여 <b>가장자리에 검은 테</b>가 돈다. 바탕화면 위에 그리는 앱이라 이 테가 그대로 보인다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 도구가 하지 <b>않는</b> 것
    /// ============================================================================
    /// <b>벡터 데이터를 한 바이트도 읽거나 쓰지 않는다</b>(<c>wornShapes</c>/<c>icon</c>).
    /// 그리고 <b>PNG가 없는 아이템의 칸을 비우지 않는다</b> — «있는 것을 연결»만 하고
    /// «없는 것을 지우는» 권한은 없다(어제 도구와 같은 규약).
    /// </summary>
    public static class WornSpriteImport
    {
        /// <summary>착용 비트맵이 사는 폴더. <b>Resources 밖</b>에 둔다 — 참조는 <c>AccessoryDefSO</c>가
        /// 직접 들고 있으므로 빌드에 포함되고, Resources 에 두면 <c>Resources.LoadAll</c>이 같은 텍스처를
        /// 한 번 더 물게 된다(§19-8-4의 상주 메모리가 두 배가 되는 자리).</summary>
        public const string SpriteFolder = "Assets/_Project/Art/WornSprites";

        /// <summary>아이템 에셋이 사는 폴더(<c>&lt;에셋이름&gt;.png</c>로 짝짓는다).</summary>
        public const string ItemFolder = "Assets/_Project/Resources/Items";

        /// <summary>뒤층 파일의 접미사 — <c>equip_head_crown_back.png</c>.</summary>
        public const string BackSuffix = "_back";

        /// <summary>규격 확인용 고정물의 접두사. 이 이름으로 시작하는 파일은 <b>아이템과 짝짓지
        /// 않는다</b>(짝이 없다고 결함으로 신고하지도 않는다).</summary>
        public const string ProbePrefix = "__";

        public const int MaxTextureSize = 256;
        public const string StandalonePlatform = "Standalone";

        /// <summary>iOS 플랫폼의 <b>API 이름</b>. <c>.meta</c>에는 <c>iOS</c>로 직렬화되지만
        /// <see cref="TextureImporter.SetPlatformTextureSettings"/>가 받는 문자열은 <c>iPhone</c>이다.
        /// 둘 다 맞다 — 확인하는 쪽(테스트)이 두 표기를 모두 받아들여야 한다.</summary>
        public const string IosPlatform = "iPhone";

        private const string LogPrefix = "[착용비트맵]";

        /// <summary>이 경로가 착용 비트맵 폴더 안인가. 후처리기와 메뉴가 <b>같은 판정</b>을 쓴다.</summary>
        public static bool IsWornSpritePath(string assetPath)
            => !string.IsNullOrEmpty(assetPath)
               && assetPath.StartsWith(SpriteFolder + "/", StringComparison.Ordinal)
               && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        // ============================================================================
        // 1. 규격 — 단일 정의
        // ============================================================================

        /// <summary>임포터 한 장을 규격으로 못박는다. <b>값의 근거는 클래스 문서에 있다.</b>
        /// <para>후처리기(자동)와 메뉴(수동 재적용)가 <b>같은 이 함수</b>를 부른다 — 두 벌이 되면
        /// «메뉴로는 맞는데 자동 임포트는 다른» 상태가 만들어지고 그건 육안으로 구분되지 않는다.</para></summary>
        public static void ApplyPreset(TextureImporter importer)
        {
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = 100f;
            settings.mipmapEnabled = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.wrapModeU = TextureWrapMode.Clamp;
            settings.wrapModeV = TextureWrapMode.Clamp;
            settings.alphaIsTransparency = true;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.sRGBTexture = true;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.readable = false;
            settings.aniso = 1;
            importer.SetTextureSettings(settings);

            importer.maxTextureSize = MaxTextureSize;
            importer.crunchedCompression = false;

            TextureImporterPlatformSettings basePlatform = importer.GetDefaultPlatformTextureSettings();
            basePlatform.maxTextureSize = MaxTextureSize;
            basePlatform.format = TextureImporterFormat.Automatic;
            basePlatform.textureCompression = TextureImporterCompression.Compressed;
            basePlatform.crunchedCompression = false;
            basePlatform.overridden = false;   // 기본 블록은 «오버라이드»가 아니라 바탕값이다.
            importer.SetPlatformTextureSettings(basePlatform);

            SetOverride(importer, StandalonePlatform, TextureImporterFormat.BC7);
            SetOverride(importer, IosPlatform, TextureImporterFormat.ASTC_6x6);
        }

        /// <summary>플랫폼 한 칸을 <b><c>overridden: 1</c>로</b> 못박는다.
        /// <para>★ 이 <c>true</c> 하나가 이 함수의 존재 이유다 — 어제 <c>.meta</c> 실측에서
        /// <c>maxTextureSize: 256</c>이 <b>적혀 있는데도</b> <c>overridden: 0</c>이라
        /// 최상위 2048이 지배하고 있었다. 「적혀 있다」와 「걸려 있다」는 다르다.</para></summary>
        private static void SetOverride(TextureImporter importer, string platform, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings(platform);
            ps.name = platform;
            ps.overridden = true;
            ps.maxTextureSize = MaxTextureSize;
            ps.format = format;
            ps.compressionQuality = (int)TextureCompressionQuality.Normal;
            ps.crunchedCompression = false;
            importer.SetPlatformTextureSettings(ps);
        }

        /// <summary>
        /// ★ 폴더에 놓이는 <b>순간</b> 규격을 건다. 사람이 메뉴를 누르는 것을 잊을 수 없게 만드는 장치다.
        /// </summary>
        private sealed class Postprocessor : AssetPostprocessor
        {
            private void OnPreprocessTexture()
            {
                if (!IsWornSpritePath(assetPath)) return;
                ApplyPreset(assetImporter as TextureImporter);
            }
        }

        // ============================================================================
        // 2. 메뉴 — 재적용 · 잉크박스 굽기 · 배선 · 확인
        // ============================================================================

        [MenuItem("StickMate/착용 비트맵/1. 임포트 규격 재적용 + 잉크박스 굽기 + 필드 배선")]
        public static void Apply() => Run(write: true);

        [MenuItem("StickMate/착용 비트맵/2. 확인만(쓰지 않음)")]
        public static void Verify() => Run(write: false);

        [MenuItem("StickMate/착용 비트맵/3. 교정 스프라이트 PNG 굽기(규격 확인용)")]
        public static void BakeCalibrationProbe()
        {
            Directory.CreateDirectory(SpriteFolder);
            string path = $"{SpriteFolder}/{ProbePrefix}calibration.png";
            Texture2D tex = WornSpriteCalibration.BuildTexture();
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"{LogPrefix} 교정 스프라이트를 굽고 규격을 걸었습니다: {path}");
        }

        /// <summary><c>-executeMethod</c> 진입점. 실패하면 <b>예외로 끝낸다</b> — 배치모드에서
        /// 종료코드만 보고 «성공»으로 읽히는 것이 이 저장소가 반복해서 당한 형태다.</summary>
        public static void ApplyBatch()
        {
            if (!Run(write: true)) throw new InvalidOperationException($"{LogPrefix} 배선 실패 — 위 로그 참조.");
        }

        private static bool Run(bool write)
        {
            var faults = new List<string>();
            string[] pngs = Directory.Exists(SpriteFolder)
                ? Directory.GetFiles(SpriteFolder, "*.png", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(pngs, StringComparer.Ordinal);

            if (pngs.Length == 0)
            {
                // ★ 결함이 아니다. 이 라운드(P0)의 정상 상태가 「그림 0장」이다 —
                //   여기서 에러를 내면 다음 사람이 «도구가 고장났다»로 읽는다.
                Debug.Log($"{LogPrefix} {SpriteFolder} 에 PNG가 없습니다 — 아직 그림이 없는 단계입니다(정상).");
                return true;
            }

            if (write)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < pngs.Length; i++)
                    {
                        string assetPath = ToUnityPath(pngs[i]);
                        if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti)
                        {
                            ApplyPreset(ti);
                            ti.SaveAndReimport();
                        }
                        else faults.Add($"{assetPath}: TextureImporter 를 못 얻었습니다.");
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.Refresh();
            }

            var report = new StringBuilder();
            report.Append($"{LogPrefix} png\t타입\t메시\t밉\t최대변\tSA오버라이드\tiOS오버라이드\t대상 에셋\t칸\t잉크박스\n");

            for (int i = 0; i < pngs.Length; i++)
            {
                string assetPath = ToUnityPath(pngs[i]);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) { faults.Add($"{assetPath}: TextureImporter 아님."); continue; }

                TextureImporterPlatformSettings sa = importer.GetPlatformTextureSettings(StandalonePlatform);
                TextureImporterPlatformSettings ios = importer.GetPlatformTextureSettings(IosPlatform);
                report.Append($"{LogPrefix} {Path.GetFileName(assetPath)}\t{importer.textureType}\t" +
                              $"{(importer.spriteImportMode == SpriteImportMode.Single ? "Single" : importer.spriteImportMode.ToString())}\t" +
                              $"{(importer.mipmapEnabled ? "on" : "off")}\t{importer.maxTextureSize}\t" +
                              $"{(sa.overridden ? sa.format.ToString() : "없음")}\t" +
                              $"{(ios.overridden ? ios.format.ToString() : "없음")}\t");

                if (fileName.StartsWith(ProbePrefix, StringComparison.Ordinal))
                {
                    report.Append("(규격 확인용 고정물)\t-\t-\n");
                    continue;
                }

                bool isBack = fileName.EndsWith(BackSuffix, StringComparison.Ordinal);
                string defName = isBack ? fileName.Substring(0, fileName.Length - BackSuffix.Length) : fileName;
                string defPath = $"{ItemFolder}/{defName}.asset";
                var def = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>(defPath);
                if (def == null)
                {
                    report.Append("(대응 에셋 없음)\t-\t-\n");
                    faults.Add($"{assetPath}: 같은 이름의 아이템 에셋 {defPath} 가 없습니다.");
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    report.Append($"{defName}\t(스프라이트 없음)\t-\n");
                    faults.Add($"{assetPath}: 스프라이트 하위 자산이 없습니다(textureType 확인).");
                    continue;
                }

                if (write)
                {
                    var so = new SerializedObject(def);
                    // ★ 필드 이름을 문자열로 베끼지 않는다 — 이름이 바뀌면 여기서 컴파일이 깨진다.
                    string field = isBack
                        ? nameof(AccessoryDefSO.wornSpriteBackOverride)
                        : nameof(AccessoryDefSO.wornSpriteOverride);
                    so.FindProperty(field).objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                }

                bool wired = isBack ? def.wornSpriteBackOverride == sprite : def.wornSpriteOverride == sprite;
                if (!wired) faults.Add($"{defPath}: {(isBack ? "wornSpriteBackOverride" : "wornSpriteOverride")} 배선 실패.");

                // ---- 잉크박스는 <b>앞장에서만</b> 굽는다 -----------------------------------
                // 뒤층은 앞층과 같은 배치 사각형을 공유하므로(렌더러가 그렇게 놓는다) 박스도 하나다.
                // 두 파일에서 각각 구우면 나중에 넣은 쪽이 앞장의 값을 덮어쓴다.
                string inkText = "-";
                if (!isBack)
                {
                    if (TryBakeInkBox(pngs[i], def, write, out Vector4 box, out string error))
                    {
                        inkText = $"({box.x:F3},{box.y:F3})-({box.z:F3},{box.w:F3})";
                    }
                    else
                    {
                        inkText = "실패";
                        faults.Add($"{assetPath}: 잉크박스 굽기 실패 — {error}");
                    }
                }
                report.Append($"{defName}\t{(wired ? (isBack ? "뒤" : "앞") : "미배선")}\t{inkText}\n");
            }

            if (write)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(report.ToString());
            Debug.Log($"{LogPrefix} png={pngs.Length} 결함={faults.Count} (write={write})");
            for (int i = 0; i < faults.Count; i++) Debug.LogError($"{LogPrefix} {faults[i]}");
            return faults.Count == 0;
        }

        // ============================================================================
        // 3. 알파 타이트 박스 굽기
        // ============================================================================

        /// <summary>
        /// PNG의 <b>원본 파일</b>을 직접 디코드해 알파 경계를 잰다.
        ///
        /// <para><b>왜 <c>sprite.texture</c>가 아니라 파일인가</b>: 임포트된 텍스처는
        /// <c>isReadable: false</c>(상주 메모리 절약)라 <c>GetPixels</c>를 못 부르고, 켜면 규격이
        /// 흔들린다. 그리고 임포트된 쪽은 <b>256으로 줄고 압축까지 된</b> 결과라 경계가 한두 픽셀
        /// 흐려진다 — 저작 원본(1024²)에서 재는 쪽이 정확하다.</para>
        ///
        /// <para><b>중간 알파는 결함으로 신고한다</b>(§19-8-2: 실루엣 밖 0 / 안 255 / 중간값 금지).
        /// 반투명 가장자리를 허용하면 임의의 바탕화면 위에서 색이 섞여 사라진다.
        /// 다만 <b>굽기는 계속한다</b> — 값을 안 넣으면 렌더러가 사각형 전체를 잉크로 보게 되어
        /// 증상이 두 개가 된다.</para>
        /// </summary>
        private static bool TryBakeInkBox(string osPath, AccessoryDefSO def, bool write,
            out Vector4 box, out string error)
        {
            box = default;
            error = null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(osPath), false))
                {
                    error = "PNG 디코드 실패";
                    return false;
                }

                Color32[] px = tex.GetPixels32();
                int w = tex.width;
                int h = tex.height;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                int partial = 0;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        byte a = px[row + x].a;
                        if (a == 0) continue;
                        if (a != 255) partial++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX < 0)
                {
                    error = "알파가 전부 0입니다(그림이 통째로 투명).";
                    return false;
                }

                if (partial > 0)
                {
                    Debug.LogError($"{LogPrefix} {Path.GetFileName(osPath)}: 반투명 픽셀 {partial}개 — " +
                        "저작 규격은 «실루엣 밖 α=0 / 안 α=255 / 중간값 금지»입니다(§19-8-2). " +
                        "반투명 가장자리는 임의의 바탕화면 위에서 색이 섞여 사라집니다.");
                }

                // 되메움 규칙은 렌더러와 <b>같은 함수</b>다 — 여기서 다른 사각형을 쓰면
                // 구운 잉크박스가 렌더러가 실제로 놓는 자리와 어긋난다(그 어긋남은 조용하다).
                Rect rectInR = WornSpritePlacement.ResolveRectInR(def.wornSpriteRectInR, h / (float)w);
                box = WornSpritePlacement.InkBoxFromNormalized(rectInR,
                    minX / (float)w, minY / (float)h, (maxX + 1) / (float)w, (maxY + 1) / (float)h);

                if (write)
                {
                    var so = new SerializedObject(def);
                    so.FindProperty(nameof(AccessoryDefSO.wornSpriteInkBoxInR)).vector4Value = box;
                    // 되메운 사각형도 함께 굳힌다 — 안 적으면 렌더러가 매번 되메우는데,
                    // 그때 잉크박스만 굳어 있으면 두 값이 서로 다른 사각형을 가리킨다.
                    so.FindProperty(nameof(AccessoryDefSO.wornSpriteRectInR)).rectValue = rectInR;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                }
                return true;
            }
            catch (Exception e)
            {
                error = $"{e.GetType().Name}: {e.Message}";
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string ToUnityPath(string osPath)
        {
            string full = Path.GetFullPath(osPath).Replace('\\', '/');
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!root.EndsWith("/", StringComparison.Ordinal)) root += "/";
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : osPath;
        }
    }
}
