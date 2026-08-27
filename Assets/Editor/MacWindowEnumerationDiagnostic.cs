#if UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using StickMate.Platform;
using StickMate.Platform.MacOS;

namespace StickMate.EditorTools
{
    /// <summary>
    /// macOS 네이티브 창 열거(MacWindowService, docs/BUG_REPORT_PHASE0.md m8 해소) 실측 검증용 도구.
    /// "컴파일된다"만으로 안심할 수 없는 P/Invoke 마샬링(특히 CoreFoundation Boolean 1바이트 문제)을
    /// 실제 macOS 세션에서 직접 호출해 확인하기 위해 작성했다 — Tasklist.md "macOS 네이티브 창 열거"
    /// 절 참고. SceneBootstrapper.cs와 같은 이유로(향후 재검증/회귀 확인 시 재사용) 작업 종료 후에도
    /// 영구 자산으로 남겨둔다.
    ///
    /// 두 부분으로 구성:
    /// 1) MacWindowService(프로덕션 클래스)를 통해 실제 IPlatformWindowService.EnumerateFootholds() 결과를
    ///    그대로 로그로 남긴다 — 이것이 진짜 검증 대상.
    /// 2) 참고용으로 CGWindowListCopyWindowInfo 원시 결과를 필터링 없이 직접 순회해 각 창의
    ///    OwnerName/WindowName/Layer까지 함께 로그로 남긴다 — MacWindowService의 비공개 필터링 로직
    ///    (kCGWindowLayer==0, 자기 자신 제외)이 실제로 무엇을 걸러내고 있는지 사람이 대조 확인할 수
    ///    있게 하기 위함이며, PlatformFoothold 자체는 원래 설계대로 제목을 노출하지 않는다
    ///    (Win32WindowService와 동일 — 창 제목은 발판 계약에 포함되지 않음).
    /// </summary>
    public static class MacWindowEnumerationDiagnostic
    {
        [MenuItem("StickMate/Diagnostics/Log macOS Window Enumeration")]
        public static void LogEnumeration()
        {
            Debug.Log("[MACWIN-TEST] ==== MacWindowService.EnumerateFootholds() 실측 시작 ====");

            var service = new MacWindowService();
            var footholds = service.EnumerateFootholds();
            Debug.Log($"[MACWIN-TEST] EnumerateFootholds() 결과 개수 = {footholds.Count}");
            for (int i = 0; i < footholds.Count; i++)
            {
                var f = footholds[i];
                Debug.Log($"[MACWIN-TEST] foothold[{i}] handle={f.Handle} rect=(x={f.ScreenRect.x:F1}, y={f.ScreenRect.y:F1}, w={f.ScreenRect.width:F1}, h={f.ScreenRect.height:F1}) isTopmost={f.IsTopmost}");
            }

            bool fullscreen = service.IsFullscreenAppActive();
            Debug.Log($"[MACWIN-TEST] IsFullscreenAppActive() = {fullscreen}");

            bool cursorOk = service.TryGetGlobalCursorPosition(out Vector2 cursor);
            Debug.Log($"[MACWIN-TEST] TryGetGlobalCursorPosition() = {cursorOk}, pos=({cursor.x:F1},{cursor.y:F1})");

            bool overlayOk = service.CreateOverlayWindow();
            Debug.Log($"[MACWIN-TEST] CreateOverlayWindow() = {overlayOk} (자기 자신의 온스크린 CGWindowID 탐색 결과 — 아래 안전가드 확인용)");

            try
            {
                service.SetClickThrough(true);
                Debug.LogError("[MACWIN-TEST] SetClickThrough()가 예외 없이 성공함 — 안전가드가 깨졌습니다!");
            }
            catch (NotSupportedException ex)
            {
                Debug.Log("[MACWIN-TEST] SetClickThrough() 안전가드 정상 동작(NotSupportedException): " + ex.Message);
            }

            try
            {
                service.SetAlwaysOnTop(true);
                Debug.LogError("[MACWIN-TEST] SetAlwaysOnTop()가 예외 없이 성공함 — 안전가드가 깨졌습니다!");
            }
            catch (NotSupportedException ex)
            {
                Debug.Log("[MACWIN-TEST] SetAlwaysOnTop() 안전가드 정상 동작(NotSupportedException): " + ex.Message);
            }

            Debug.Log("[MACWIN-TEST] ==== 원시(raw, 필터링 없음) CGWindowListCopyWindowInfo 대조 덤프 시작 ====");
            LogRawWindowList();

            Debug.Log("[MACWIN-TEST] ==== 완료 ====");
        }

        #region 원시 대조 덤프 전용 P/Invoke (MacWindowService.cs의 비공개 선언과 별개 — 진단 전용, 프로덕션 미사용)
        private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint { public double X; public double Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CGSize { public double Width; public double Height; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CGRect { public CGPoint Origin; public CGSize Size; }

        private const uint kCGWindowListOptionOnScreenOnly = 1u << 0;
        private const uint kCGNullWindowID = 0;
        private const uint kCFStringEncodingUTF8 = 0x08000100;
        private const int kCFNumberSInt32Type = 3;

        [DllImport(CoreGraphicsLib)]
        private static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
        [DllImport(CoreGraphicsLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);
        [DllImport(CoreFoundationLib)]
        private static extern long CFArrayGetCount(IntPtr theArray);
        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);
        [DllImport(CoreFoundationLib)]
        private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);
        [DllImport(CoreFoundationLib, CharSet = CharSet.Ansi)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);
        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);
        [DllImport(CoreFoundationLib)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);
        [DllImport(CoreFoundationLib)]
        private static extern void CFRelease(IntPtr cf);

        private static bool TryGetInt(IntPtr dict, IntPtr key, out int value)
        {
            value = 0;
            IntPtr numberRef = CFDictionaryGetValue(dict, key);
            if (numberRef == IntPtr.Zero) return false;
            return CFNumberGetValue(numberRef, kCFNumberSInt32Type, out value);
        }

        private static string TryGetString(IntPtr dict, IntPtr key, byte[] buffer)
        {
            IntPtr stringRef = CFDictionaryGetValue(dict, key);
            if (stringRef == IntPtr.Zero) return "(null)";
            if (!CFStringGetCString(stringRef, buffer, buffer.Length, kCFStringEncodingUTF8)) return "(unreadable)";
            int len = Array.IndexOf(buffer, (byte)0);
            if (len < 0) len = buffer.Length;
            return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
        }

        private static void LogRawWindowList()
        {
            IntPtr keyOwnerName = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerName", kCFStringEncodingUTF8);
            IntPtr keyWindowName = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowName", kCFStringEncodingUTF8);
            IntPtr keyLayer = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowLayer", kCFStringEncodingUTF8);
            IntPtr keyBounds = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowBounds", kCFStringEncodingUTF8);
            IntPtr keyOwnerPid = CFStringCreateWithCString(IntPtr.Zero, "kCGWindowOwnerPID", kCFStringEncodingUTF8);
            var buffer = new byte[256];

            IntPtr windowArray = CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly, kCGNullWindowID);
            if (windowArray == IntPtr.Zero)
            {
                Debug.LogWarning("[MACWIN-TEST] 원시 덤프: CGWindowListCopyWindowInfo가 NULL을 반환했습니다.");
                return;
            }

            try
            {
                long count = CFArrayGetCount(windowArray);
                Debug.Log($"[MACWIN-TEST] 원시 온스크린 창 총 개수(필터링 전) = {count}");
                for (long i = 0; i < count; i++)
                {
                    IntPtr dict = CFArrayGetValueAtIndex(windowArray, i);
                    if (dict == IntPtr.Zero) continue;

                    string ownerName = TryGetString(dict, keyOwnerName, buffer);
                    string windowName = TryGetString(dict, keyWindowName, buffer);
                    TryGetInt(dict, keyLayer, out int layer);
                    TryGetInt(dict, keyOwnerPid, out int ownerPid);

                    string rectStr = "(bounds 읽기 실패)";
                    IntPtr boundsDict = CFDictionaryGetValue(dict, keyBounds);
                    if (boundsDict != IntPtr.Zero && CGRectMakeWithDictionaryRepresentation(boundsDict, out CGRect r))
                    {
                        rectStr = $"(x={r.Origin.X:F0}, y={r.Origin.Y:F0}, w={r.Size.Width:F0}, h={r.Size.Height:F0})";
                    }

                    Debug.Log($"[MACWIN-TEST] raw[{i}] owner=\"{ownerName}\"(pid={ownerPid}) title=\"{windowName}\" layer={layer} bounds={rectStr}");
                }
            }
            finally
            {
                CFRelease(windowArray);
            }
        }
        #endregion
    }
}
#endif
