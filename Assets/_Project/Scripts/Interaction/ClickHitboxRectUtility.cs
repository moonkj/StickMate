using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// Collider2D의 월드 바운즈를 OS 화면 좌표(Platform.ILocalClickCaptureService가 요구하는 좌표계)로
    /// 변환하는 공용 유틸. DragThrowController가 "캐릭터 히트박스의 현재
    /// OS 화면 사각형"을 계산해야 해서(부분적 클릭관통 해제 요청의 hitboxOsScreen 인자) 이 유틸로 분리한다
    /// — Platform/ScreenCoordinateConverter.cs의 좌표 변환 컨벤션을 그대로 재사용한다.
    /// </summary>
    internal static class ClickHitboxRectUtility
    {
        public static Rect ComputeOsRect(Collider2D collider, Camera cam, StickConfig config)
        {
            if (collider == null || cam == null) return default;

            Bounds b = collider.bounds;
            Vector2 topLeftOs = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector3(b.min.x, b.max.y, 0f), config, out _);
            Vector2 bottomRightOs = ScreenCoordinateConverter.WorldToOsScreen(cam, new Vector3(b.max.x, b.min.y, 0f), config, out _);

            float x = Mathf.Min(topLeftOs.x, bottomRightOs.x);
            float y = Mathf.Min(topLeftOs.y, bottomRightOs.y);
            float w = Mathf.Abs(bottomRightOs.x - topLeftOs.x);
            float h = Mathf.Abs(bottomRightOs.y - topLeftOs.y);
            return new Rect(x, y, w, h);
        }
    }
}
