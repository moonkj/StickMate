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
    /// ★ <b>팩 카드 비트맵 배선 도구</b>(2026-09-08) — 이 저장소 최초의 래스터 이미지 자산을
    /// 카드 표면에 연결한다. 하는 일은 둘뿐이고, 둘 다 <b>되돌려 부를 수 있다</b>(멱등).
    ///
    /// <list type="number">
    ///   <item><b>임포트 설정</b> — <c>Resources/Items/Icons/*.png</c>를 <see cref="TextureImporterType.Sprite"/>·
    ///     <see cref="FilterMode.Bilinear"/>·<b>무압축</b>으로 못박는다.</item>
    ///   <item><b>필드 채우기</b> — 같은 이름의 <c>Resources/Items/&lt;같은이름&gt;.asset</c>의
    ///     <see cref="AccessoryDefSO.cardIconOverride"/>에 그 스프라이트를 넣는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 왜 «손으로 쓴 .meta»가 아니라 도구인가
    /// ============================================================================
    /// 이 프로젝트에는 <b>텍스처 <c>.meta</c>가 한 장도 없었다</b>(2026-09-08 실측: <c>Assets</c> 전체에
    /// PNG/JPG/PSD 0개). 즉 베낄 본이 없고, <c>TextureImporter</c>의 <c>serializedVersion</c>을 잘못 적으면
    /// Unity가 <b>조용히 다른 뜻으로</b> 읽는다. 그래서 <b>Unity 자신에게 쓰게 한다</b> —
    /// 그러면 <c>.meta</c>는 정의상 이 에디터 버전이 이해하는 정규형이다.
    ///
    /// ============================================================================
    /// ★ 임포트 값의 근거 (숫자를 지어내지 않았다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>Sprite(2D and UI)</b> — 이 프로젝트는 <c>EditorSettings.m_DefaultBehaviorMode = 0</c>(3D)이라
    ///     <b>가만 두면 <c>Default</c> 타입으로 들어온다</b>. 그러면 <c>Image.sprite</c>에 넣을 스프라이트가
    ///     아예 생성되지 않는다 — 「그림이 안 나온다」의 가장 조용한 원인이다.</item>
    ///   <item><b>Bilinear</b> — 사용자 지시(그라데이션이라 <c>Point</c>는 안 된다).</item>
    ///   <item><b>maxTextureSize 256</b> — 카드에서 실제로 그려지는 크기는 <b>72pt</b>이고
    ///     (<c>CharacterInfoWindow.BitmapIconSize</c>), 이 창의 캔버스는 <c>ConstantPixelSize</c>에
    ///     DPI 배율을 그대로 넣는다. Retina(×2)에서 <b>144물리픽셀</b>이므로 256이면 축소 1.78배로
    ///     충분히 선명하고, 원본 512는 그 위로 <b>한 픽셀도 화면에 닿지 않는다</b>(순수 낭비 ×4).</item>
    ///   <item><b>밉맵 켬</b> — ×1 DPI에서는 같은 그림이 <b>72물리픽셀</b>이라 256 → 72는 3.56배 축소다.
    ///     밉맵이 없으면 그 배율에서 <b>언더샘플링</b>이 나고, 이 그림들은 얇은 윤곽선을 갖고 있어
    ///     증상이 «선이 끊겨 보인다»로 나타난다. 비용은 +33% 메모리다.</item>
    ///   <item><b>무압축(RGBA32)</b> — 사용자 지시 «무손실 선호». 그리고 <b>플랫폼 패리티</b>가 걸려 있다:
    ///     압축을 켜면 Windows(DXT5/BC7)와 macOS(같은 BC 계열이지만 Apple Silicon에서는 ASTC 경로가
    ///     열린다)가 <b>서로 다른 손실</b>을 낸다. 그라데이션·금속광택이 주인공인 그림이라 그 차이가
    ///     밴딩으로 보인다. 무압축은 <b>두 플랫폼이 같은 비트</b>다.
    ///     ★ <b>실측(2026-09-08)</b>: 12장 전부 <c>256×256 RGB24</c>로 들어온다(원본 PNG 12장 모두
    ///     알파 채널이 없다 — 컬러타입 2). 256²×3B×1.33(밉) × 12 ≈ <b>3.1MB</b>.
    ///     <c>RGB24</c>는 블록 압축이 아니라 <b>플랫폼 열거값이 하나</b>다.</item>
    ///   <item><b>FullRect 메시</b> — uGUI <c>Image</c>용 권장값. <c>Tight</c>는 사각형 UI에서 정점만 늘린다.</item>
    ///   <item><b>플랫폼 오버라이드 끔</b> — <c>Standalone</c> 오버라이드를 <c>overridden: false</c>로
    ///     끈다(2026-09-08 실측 정정: <c>ClearPlatformTextureSettings</c>는 블록을 지우지 못하고
    ///     <c>overridden</c>만 꺼진 채 남는다 — 결과는 같다, 값이 꺼져 있으면 기본
    ///     <c>DefaultTexturePlatform</c> 설정을 그대로 따른다). 켜진 채 남으면 한 플랫폼만 다른
    ///     포맷으로 굳고, 그 차이는 <b>그 플랫폼에서 빌드해야만</b> 보인다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 도구가 하지 <b>않는</b> 것
    /// ============================================================================
    /// <b>몸에 붙는 그림은 건드리지 않는다.</b> <c>wornShapes</c>는 한 바이트도 읽지도 쓰지도 않는다
    /// (2026-09-08 리더 판단 — 캐릭터가 부위별로 다른 화법이 되는 것을 막는다).
    /// 그리고 <b>PNG가 없는 아이템의 필드를 비우지도 않는다</b> — 이 도구는 «있는 것을 연결»만 하고
    /// «없는 것을 지우는» 권한은 없다. 드리프트 판정은 <c>PackCardBitmapIconTests</c>가 한다.
    /// </summary>
    public static class PackCardIconImport
    {
        private const string ItemFolder = "Assets/_Project/Resources/Items";
        private const string IconFolder = ItemFolder + "/Icons";

        /// <summary>카드에 그려지는 최대 물리 픽셀(72pt × Retina 2배 = 144)보다 크고 2의 거듭제곱인 첫 값.
        /// <b>512를 그대로 두면 화면에 닿지 않는 픽셀을 4배로 싣는다.</b></summary>
        private const int MaxTextureSize = 256;

        [MenuItem("StickMate/팩 카드 아이콘/1. 임포트 설정 + 필드 배선")]
        public static void Apply() => Run(write: true);

        [MenuItem("StickMate/팩 카드 아이콘/2. 확인만(쓰지 않음)")]
        public static void Verify() => Run(write: false);

        /// <summary><c>-executeMethod</c> 진입점. 실패하면 <b>예외로 끝낸다</b> — 배치모드에서
        /// 종료코드만 보고 «성공»으로 읽히는 것이 이 저장소가 열세 번 당한 형태다.</summary>
        public static void ApplyBatch()
        {
            if (!Run(write: true)) throw new InvalidOperationException("[팩카드] 배선 실패 — 위 로그 참조.");
        }

        private static bool Run(bool write)
        {
            string[] pngs = Directory.Exists(IconFolder)
                ? Directory.GetFiles(IconFolder, "*.png", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            Array.Sort(pngs, StringComparer.Ordinal);

            if (pngs.Length == 0)
            {
                Debug.LogError($"[팩카드] {IconFolder} 에 PNG가 하나도 없습니다. 경로를 확인하세요.");
                return false;
            }

            var faults = new List<string>();

            // ---- 1차: 임포트 설정 ----------------------------------------------------------
            // ★ <b>스프라이트를 읽기 전에 반드시 끝나야 한다.</b> 타입이 Sprite 가 되기 전에는
            //   <c>LoadAssetAtPath&lt;Sprite&gt;</c>가 <b>null</b>이고, 그 null 은 「파일이 없다」와
            //   출력이 똑같이 생겼다. 그래서 배치 구간을 <b>닫고</b> 2차로 넘어간다.
            if (write)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string osPath in pngs)
                    {
                        string assetPath = ToUnityPath(osPath);
                        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer) Configure(importer);
                        else faults.Add($"{assetPath}: TextureImporter 를 못 얻었습니다(아직 임포트되지 않음?).");
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.Refresh();
            }

            // ---- 2차: 스프라이트 -> 아이템 에셋 필드 --------------------------------------
            var report = new StringBuilder();
            report.Append("[팩카드] png\t타입\t필터\t밉\t최대변\t압축\t대상 에셋\t배선\t실제크기\t실제포맷\n");
            foreach (string osPath in pngs)
            {
                string assetPath = ToUnityPath(osPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    faults.Add($"{assetPath}: TextureImporter 를 못 얻었습니다.");
                    continue;
                }

                report.Append($"[팩카드] {Path.GetFileName(assetPath)}\t{importer.textureType}\t" +
                              $"{importer.filterMode}\t{(importer.mipmapEnabled ? "on" : "off")}\t" +
                              $"{importer.maxTextureSize}\t{importer.textureCompression}\t");

                string defPath = $"{ItemFolder}/{Path.GetFileNameWithoutExtension(assetPath)}.asset";
                var def = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>(defPath);
                if (def == null)
                {
                    report.Append("(대응 에셋 없음)\t-\n");
                    faults.Add($"{assetPath}: 같은 이름의 아이템 에셋 {defPath} 가 없습니다.");
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    report.Append($"{Path.GetFileName(defPath)}\t(스프라이트 없음)\n");
                    faults.Add($"{assetPath}: 스프라이트 하위 자산이 없습니다(textureType 이 Sprite 인지 확인).");
                    continue;
                }

                if (write)
                {
                    var so = new SerializedObject(def);
                    // ★ 문자열을 베끼지 않는다 — 필드 이름이 바뀌면 여기서 <b>컴파일이</b> 깨진다.
                    SerializedProperty prop = so.FindProperty(nameof(AccessoryDefSO.cardIconOverride));
                    prop.objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(def);
                }

                bool wired = def.cardIconOverride == sprite;
                // ★ 실제로 임포트된 텍스처의 <b>크기와 포맷</b>을 함께 찍는다. 임포터 설정은 «요청»이고
                //   이쪽이 «결과»다 — 둘을 같이 보지 않으면 «256으로 적었는데 512로 들어왔다»를 못 본다.
                Texture2D tex = sprite.texture;
                report.Append($"{Path.GetFileName(defPath)}\t{(wired ? "OK" : "미배선")}\t" +
                              $"{tex.width}x{tex.height}\t{tex.format}\n");
                // ★ 2026-09-08 수정 — 예전엔 `write &&`가 앞에 붙어 있어 "2. 확인만(쓰지 않음)" 메뉴가
                //   배선이 진짜로 끊겨 있어도(예: 누가 필드를 나중에 지웠다) 항상 결함=0을 찍는
                //   죽은 프로브였다(verify-change 2026-09-08 재검증으로 발견). write 여부와 무관하게
                //   "지금 배선이 되어 있는가"는 항상 참인 사실이므로 무조건 검사한다.
                if (!wired) faults.Add($"{defPath}: cardIconOverride 배선에 실패했습니다.");
            }

            if (write)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(report.ToString());
            Debug.Log($"[팩카드] png={pngs.Length} 결함={faults.Count} (write={write})");
            for (int i = 0; i < faults.Count; i++) Debug.LogError($"[팩카드] {faults[i]}");
            return faults.Count == 0;
        }

        /// <summary>임포터 한 장을 못박는다. <b>값의 근거는 클래스 문서에 있다.</b></summary>
        private static void Configure(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
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
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = MaxTextureSize;
            platform.format = TextureImporterFormat.Automatic;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            platform.overridden = false;
            importer.SetPlatformTextureSettings(platform);

            // ★ 플랫폼 오버라이드가 남아 있으면 Windows 와 macOS 가 서로 다른 포맷으로 굳는다.
            //   실제로 이 라운드의 «Windows 영향: 없음»을 구조적으로 만드는 것은 위
            //   <c>platform.overridden = false</c>다(.meta 실측: Standalone 블록 자체는 남고
            //   `overridden: 0`만 찍힌다 — 이 호출이 블록을 지우지는 못한다). 그래도 <b>불러 둔다</b>:
            //   미래에 다른 코드가 같은 임포터에 실제 오버라이드를 심어도, 이 함수가 다시 돌면
            //   그 오버라이드까지 지우려 시도하는 쪽이 아무것도 안 하는 쪽보다 안전하다.
            importer.ClearPlatformTextureSettings("Standalone");

            importer.SaveAndReimport();
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
