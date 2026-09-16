// Màn che chuyển cảnh: một tấm phủ kín màn hình, hiện dần theo một ẢNH MASK thang xám
// thay vì mờ đều.
//
// Mọi hình dáng quét đều nằm ở ảnh mask, không nằm trong shader: chéo, tròn, sọc, tan
// hạt — đổi ảnh là đổi kiểu quét, không phải sửa một dòng code nào. Shader chỉ làm đúng
// một việc: so độ xám của mask với một ngưỡng đang chạy.
//
// Ngưỡng ăn theo công thức c = lerp(-s, 1+s, _Cutoff) chứ không phải chính _Cutoff. Nếu
// so thẳng thì ở _Cutoff = 1 những điểm có độ xám đúng bằng 1 vẫn chưa bị phủ, và màn
// hình còn sót một vệt trong suốt ngay lúc đáng lẽ đã che kín — chỗ duy nhất mà cả màn
// chuyển bị lộ.
Shader "JewelPainter/UI/ScreenTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Transition Mask (thang xám)", 2D) = "white" {}
        _Color ("Màu tấm che", Color) = (0,0,0,1)
        _Cutoff ("Cutoff (0 = trong suốt, 1 = che kín)", Range(0,1)) = 0
        _Softness ("Độ mềm của mép", Range(0.001,0.5)) = 0.08
        [Toggle] _Invert ("Đảo mask", Float) = 0
        [Toggle(_ASPECTCORRECT_ON)] _AspectCorrect ("Giữ đúng hình (cho mask tròn)", Float) = 0
        [HideInInspector] _Aspect ("Aspect (rộng/cao)", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma shader_feature_local _ASPECTCORRECT_ON

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            fixed4 _Color;
            float _Cutoff;
            float _Softness;
            float _Invert;
            float _Aspect;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MaskTex);
                OUT.color = v.color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                #ifdef _ASPECTCORRECT_ON
                // Ảnh mask vuông bị kéo giãn theo khung nhìn, nên một vòng tròn vẽ trong
                // ảnh ra màn hình dọc thành hình bầu dục. Ở đây co toạ độ quanh tâm để
                // một đơn vị theo trục ngang và một đơn vị theo trục dọc ứng với CÙNG một
                // khoảng cách pixel — vòng tròn trở lại tròn ở mọi tỉ lệ màn.
                //
                // Quy về cạnh DÀI: hình vuông cạnh max(rộng, cao) phủ trọn màn hình, nên
                // toạ độ sau khi co không bao giờ ra ngoài [0, 1] và không phụ thuộc kiểu
                // wrap của texture.
                //
                // Chỉ bật cho mask có hình (tròn, sao, cánh hoa). Mask quét thẳng hay quét
                // chéo thì bật lên chỉ làm đổi độ nghiêng.
                float2 scale = _Aspect >= 1.0 ? float2(1.0, 1.0 / _Aspect) : float2(_Aspect, 1.0);
                uv = 0.5 + (uv - 0.5) * scale;
                #endif

                // Chỉ đọc kênh đỏ: ảnh mask là thang xám nên ba kênh bằng nhau, lấy một
                // kênh là đủ và khỏi phải nhân trọng số luminance cho thứ vốn đã xám.
                half m = tex2D(_MaskTex, uv).r;
                m = lerp(m, 1.0 - m, _Invert);

                half s = max(_Softness, 1e-4);

                // Nới ngưỡng ra ngoài đoạn [0, 1] đúng bằng bề rộng mép mềm ở cả hai đầu:
                // _Cutoff = 0 phải trong suốt TUYỆT ĐỐI, = 1 phải che kín TUYỆT ĐỐI.
                half c = lerp(-s, 1.0 + s, _Cutoff);
                half a = saturate((c - m) / s);

                fixed4 color = _Color * IN.color;
                color.a *= a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
