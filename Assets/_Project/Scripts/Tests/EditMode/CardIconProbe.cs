using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 카드 아이콘 <b>실제 경로</b> 렌더 탐침 — 테스트가 아니라 <c>-executeMethod</c> 진입점이다 (2026-09-05).
    ///
    /// <para>「컴파일 통과」를 「그림 일치」로 착각한 사고가 이 저장소에 두 번 있었다. 그래서 카드를
    /// <see cref="AccessoryCardIcon.TryBuild"/> <b>그대로</b>(uGUI Image 조각 → 카메라 → RenderTexture) 찍어
    /// PNG 로 남기고, 오프라인 시트(<c>design/equipment/verify/r15_compare_sheet.png</c> B 열)와 나란히 놓는다.
    /// 합성은 <c>Tools/CardShapeGen/render_card_sheet.py</c> 가 한다.</para>
    ///
    /// <code>
    /// STICKMATE_CARD_PROBE_DIR=/abs/out Unity -batchmode -projectPath . \
    ///   -executeMethod StickMate.Tests.EditMode.CardIconProbe.Render -quit -logFile probe.log
    /// </code>
    /// <para>★ <c>-nographics</c> 를 주면 카메라가 아무것도 못 그린다 — 그때는 <b>실패로 끝낸다</b>(빈 PNG 를 남기지 않는다).
    /// 이 프로젝트는 내장 파이프라인(GraphicsSettings.m_CustomRenderPipeline = 0)이라 <see cref="Camera.Render"/> 가 곧 렌더다.</para>
    /// <para>두 색 변종을 찍는다: <c>brass</c>(주색=보조색=인계본 브라스 #C8A15A — 시트 B 열과 1:1 대조용) /
    /// <c>prod</c>(카탈로그 재질색 — 실제로 유저가 보는 색).</para>
    /// </summary>
    public static class CardIconProbe
    {
        private const string EnvDir = "STICKMATE_CARD_PROBE_DIR";
        private static readonly int[] Sizes = { 232, 58 };            // 58px 카드의 4배(시트 셀) / 실제 크기
        private static readonly Color Brass = new Color(0xC8 / 255f, 0xA1 / 255f, 0x5A / 255f, 1f);

        /// <summary>인계본은 망토 두 종만 강조색이 아니라 <b>재질색</b> <c>#D2402F</c>로 그린다(r15_model.MAT).
        /// 시트 B 열과 1:1 이 되려면 brass 변종도 그 둘은 이 색을 넣어야 한다.</summary>
        private static readonly Color HandoffCapeMaterial = new Color(0xD2 / 255f, 0x40 / 255f, 0x2F / 255f, 1f);

        private static bool IsHandoffCape(EquipmentSlot slot, int item)
            => slot == EquipmentSlot.Shoulders
               && (item == AccessoryShapeBuilder.BackCape || item == AccessoryShapeBuilder.BackLongCape);

        private static readonly EquipmentSlot[] Slots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        public static void Render()
        {
            string dir = Environment.GetEnvironmentVariable(EnvDir);
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException($"{EnvDir} 환경변수가 비었습니다.");
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("그래픽 장치가 Null 입니다(-nographics?). 카메라가 아무것도 못 그리므로 중단합니다.");
            Directory.CreateDirectory(dir);

            var manifest = new StringBuilder();
            manifest.Append("# variant\tsize\tslot\titem\tid\tdesigned\tfile\n");
            int written = 0;
            foreach (int size in Sizes)
            {
                foreach (string variant in new[] { "brass", "prod" })
                {
                    foreach (EquipmentSlot slot in Slots)
                    {
                        for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                        {
                            ItemCatalogEntry entry = ItemCatalog.Item(slot, item);
                            if (entry == null) continue;
                            Color sheetInput = Brass;   // 재질 팔레트 뒤로는 망토도 재질색 M(카탈로그) — brass 변종은 전부 브라스로 고정한다
                            Color primary = variant == "brass" ? sheetInput : entry.PrimaryColor;
                            Color secondary = variant == "brass" ? sheetInput : entry.SecondaryColor;
                            string file = $"{variant}_{size}_{EquipmentModel.SlotCode(slot)}_{item}_{entry.Id.Replace('.', '_')}.png";
                            bool designed = RenderOne(Path.Combine(dir, file), slot, item, size, primary, secondary);
                            manifest.Append($"{variant}\t{size}\t{EquipmentModel.SlotCode(slot)}\t{item}\t{entry.Id}\t{(designed ? 1 : 0)}\t{file}\n");
                            written++;
                        }
                    }
                }
            }
            File.WriteAllText(Path.Combine(dir, "manifest.tsv"), manifest.ToString(), new UTF8Encoding(false));
            Debug.Log($"[CardIconProbe] {written}장 기록: {dir}");
        }

        /// <summary>카드 하나를 <paramref name="size"/>² RenderTexture 에 찍는다. 반환: 카드 변형(설계된 카드) 경로였는가.</summary>
        private static bool RenderOne(string path, EquipmentSlot slot, int item, int size, Color primary, Color secondary)
        {
            var camGo = new GameObject("CardProbeCamera");
            var canvasGo = new GameObject("CardProbeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = size * 0.5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = UiChrome.CardSurfaceMuted;   // 워시 합성이 전제한 바탕(§13-3-1)
                cam.cullingMask = -1;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;

                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;

                var rootGo = new GameObject("Icon", typeof(RectTransform));
                rootGo.transform.SetParent(canvasGo.transform, false);
                var root = rootGo.GetComponent<RectTransform>();
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
                root.sizeDelta = new Vector2(size, size);
                root.anchoredPosition = Vector2.zero;

                // 폴백 획: CharacterInfoWindow.IconStroke(1.7 × IconSize/40)와 같은 비율을 size 에 맞춘다.
                float fallbackStroke = 1.7f * (size / 40f);
                bool built = AccessoryCardIcon.TryBuild(root, slot, item, size, fallbackStroke, primary, secondary);
                if (!built) throw new InvalidOperationException($"{slot} {item}: TryBuild 가 false 입니다.");

                var shapes = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(shapes, slot, item, AccessoryCardIcon.CardRig(),
                    float.PositiveInfinity, 0f, false, AccessorySurface.Card);
                bool designed = AccessoryCardIcon.HasDesignedCard(shapes);

                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return designed;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null)
                {
                    Camera cam = camGo.GetComponent<Camera>();
                    if (cam != null) cam.targetTexture = null;   // 물린 채 Release 하면 경고가 난다
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }
    }
}
