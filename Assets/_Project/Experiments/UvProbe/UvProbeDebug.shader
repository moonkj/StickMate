// =============================================================================
// UvProbeDebug — perf-doc R9 실험 전용. 프로덕션 경로에서 참조하지 않는다.
//
// 목적 하나: LineRenderer가 굽는 메시에서 uv.y가 "폭 방향 0..1"을 지키는가를
//           캡(numCapVertices=8) · 코너(numCornerVertices=8) 팬에서 눈으로 본다.
//
//  _Mode 0 : uv를 그대로 색으로 (R=uv.x, G=uv.y, B=0)  ← 리더가 지정한 그 실험
//  _Mode 1 : uv.y만 회색조로 (0=검, 1=흰)
//  _Mode 2 : 대안 A(프래그먼트 밴드) 에뮬레이션 —
//            |uv.y-0.5| < _CoreHalf 이면 잉크색, 아니면 역잉크(막)
//  _Mode 3 : 대안 A'(A + 끝면 밴드). numCapVertices=0 과 짝으로만 뜻이 있다.
//            uv.y 밴드에 더해 uv.x 양 끝에도 막을 만든다. 두께는 화면 기준으로
//            자기 교정한다 — 쿼드 본문에서 |grad uv.y| = 1/(2 rho_px) 이므로
//            막 두께(px) = (0.5 - _CoreHalf) / |grad uv.y| 이고,
//            그것을 |grad uv.x| 로 되곱해 uv.x 문턱을 만든다. 유니폼이 없어도 된다.
//  _Mode 4 : 단색 잉크(막 없음). ★ 조형 대가를 셰이딩과 분리해 재는 대조군.
//
// ※ 이 셰이더는 어떤 씬·프리팹·Resources도 참조하지 않으므로 빌드에 포함되지 않는다.
// =============================================================================
Shader "StickMate/Debug/UvProbe"
{
    Properties
    {
        _Mode ("Mode (0=uv,1=v gray,2=A band,3=A' band+ends,4=solid ink)", Float) = 0
        _CoreHalf ("Core half in uv units", Float) = 0.5
        _Ink ("Ink color", Color) = (0,0,0,1)
        _Membrane ("Membrane color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        ZTest Always
        Lighting Off
        Fog { Mode Off }
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float  _Mode;
            float  _CoreHalf;
            fixed4 _Ink;
            fixed4 _Membrane;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Mode < 0.5)      return fixed4(i.uv.x, i.uv.y, 0, 1);
                else if (_Mode < 1.5) return fixed4(i.uv.y, i.uv.y, i.uv.y, 1);
                if (_Mode < 2.5) return (abs(i.uv.y - 0.5) < _CoreHalf) ? _Ink : _Membrane;
                if (_Mode > 3.5) return _Ink;                     // 모드 4: 단색 대조군

                // 모드 3 — A'
                float2 gv2 = float2(ddx(i.uv.y), ddy(i.uv.y));
                float2 gu2 = float2(ddx(i.uv.x), ddy(i.uv.x));
                float  gv  = length(gv2);
                float  gu  = length(gu2);
                float  memPx = (gv > 1e-8) ? (0.5 - _CoreHalf) / gv : 0.0;   // 막 두께(화면 픽셀)
                float  ux    = memPx * gu;                                    // 같은 두께를 uv.x 단위로
                bool   sideMem = abs(i.uv.y - 0.5) >= _CoreHalf;
                bool   endMem  = (i.uv.x < ux) || (i.uv.x > 1.0 - ux);
                return (sideMem || endMem) ? _Membrane : _Ink;
            }
            ENDCG
        }
    }
    Fallback Off
}
