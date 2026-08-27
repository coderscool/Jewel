// Viên ngọc một lượt vẽ: mỗi mặt cắt là một PHÉP CHỈNH chạy lên màu ô, không phải
// một lớp xám phủ lên.
//
// Vì sao đổi khỏi lớp xám: mặt tối của viên ngọc mẫu RỰC HƠN màu ô (#0096FF trên
// nền #81C5FF). Phủ xám lên chỉ đổi được sáng tối chứ giữ nguyên độ rực, nên mặt
// đáy bao giờ cũng ra một màu nhợt hơn bản mẫu, kéo núm cỡ nào cũng không tới.
// Bộ ba số thì đổi được độ rực, và nó đúng là phép toán ColorAdjustment mà
// LevelManager đã chạy — cùng một ngôn ngữ, không phải học thêm thang số mới.
//
// _MainTex: hình bóng viên ngọc, chỉ dùng kênh alpha. Sprite gắn trên renderer.
// _ParamTex: bản đồ tham số. R = độ rực, G = độ tương phản, B = độ sáng, A không dùng.
//
// _ParamTex PHẢI TẮT sRGB VÀ TẮT NÉN. Ba kênh đó là SỐ, không phải màu: bật sRGB
// thì Unity bẻ cong giá trị theo đường gamma, bật nén thì nó xô lệch từng khối 4x4.
// Cả hai đều làm mặt cắt ra màu sai mà không báo lỗi gì.
//
// HAI TEXTURE PHẢI CÙNG HỆ TOẠ ĐỘ UV — đừng nhét sprite ngọc vào Sprite Atlas.
Shader "JewelPainter/Jewel Facets"
{
    Properties
    {
        [PerRendererData] _MainTex ("Hình bóng (Sprite)", 2D) = "white" {}
        [NoScaleOffset] _ParamTex ("Bản đồ mặt cắt (R rực, G tương phản, B sáng)", 2D) = "grey" {}
        _FacetStrength ("Độ đậm mặt cắt", Range(0, 1)) = 1
        _HighlightWhite ("Độ trắng mặt đỉnh", Range(0, 1)) = 1

        [Header(Chinh chung cho ca vien ngoc)]
        _Saturation ("Độ rực", Range(-1, 1)) = 0
        _Contrast ("Độ tương phản", Range(-1, 1)) = 0
        _Brightness ("Độ sáng", Range(-0.5, 0.5)) = 0

        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            // Thang mã hoá độ rực trong kênh R. Phải khớp S_MIN/S_MAX của script sinh ảnh.
            #define SAT_MIN -1.0
            #define SAT_MAX  3.0

            // Mức pha trắng mà núm _HighlightWhite KHÔNG đụng tới.
            //
            // Mặt bàn pha trắng 0.445, mặt đỉnh 0.885. Đặt sàn ở giữa thì núm chỉ ăn
            // vào phần vượt sàn, tức gần như chỉ ăn vào mặt đỉnh — hạ độ trắng của
            // đỉnh mà mặt bàn giữ nguyên. Nhân đều cả hai thì viên ngọc phẳng đi chứ
            // không phải bớt loé.
            #define HIGHLIGHT_FLOOR 0.5

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _RendererColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _ParamTex;
            float _FacetStrength;
            float _HighlightWhite;
            float _Saturation;
            float _Contrast;
            float _Brightness;

            // Bản shader của ColorAdjustment.Apply. Rực -> tương phản -> sáng, đúng
            // thứ tự Photoshop; đảo hai bước đầu cho ra kết quả khác hẳn vì tương phản
            // xoay quanh mốc 0.5 nên nó khuếch đại luôn phần lệch mà bước tăng rực
            // vừa tạo ra.
            float3 AdjustColor(float3 c, float s, float k, float b)
            {
                // Trọng số Rec. 601, giống hằng số trong ColorAdjustment.cs.
                float gray = dot(c, float3(0.299, 0.587, 0.114));

                float3 v = gray + (c - gray) * (1.0 + s);
                v = (v - 0.5) * (1.0 + k) + 0.5;

                return saturate(v + b);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed silhouette = tex2D(_MainTex, IN.texcoord).a;
                float3 p = tex2D(_ParamTex, IN.texcoord).rgb;

                // Bộ số của mặt cắt, rồi cộng thêm bộ số chung của cả viên.
                // _FacetStrength = 0 làm phần mặt cắt biến mất, còn lại đúng màu ô.
                float s = lerp(SAT_MIN, SAT_MAX, p.r) * _FacetStrength;
                float k = (p.g * 2.0 - 1.0) * _FacetStrength;
                float b = (p.b - 0.5) * _FacetStrength;

                // Mọi mặt trong ảnh tham số đều là phép PHA THEO TỈ LỆ: pha về trắng
                // một lượng t thì (k, b) = (-t, +t/2), pha về đen thì (-t, -t/2).
                // Nên dấu của b cho biết mặt này đang pha về đâu, và -k chính là t.
                // Nhờ vậy núm dưới đây tách được riêng mấy mặt loé mà không cần ảnh
                // tham số mang thêm một kênh đánh dấu.
                float towardWhite = step(0.0001, b);
                float t = -k;
                float trimmed = t - max(0.0, t - HIGHLIGHT_FLOOR) * (1.0 - _HighlightWhite);

                k = lerp(k, -trimmed, towardWhite);
                b = lerp(b, 0.5 * trimmed, towardWhite);

                // Bộ số chung của cả viên, cộng sau cùng.
                s += _Saturation;
                k += _Contrast;
                b += _Brightness;

                float3 rgb = IN.color.rgb;

                // Phép chỉnh làm trong hệ gamma.
                //
                // Không phải tuỳ chọn thẩm mỹ: bộ số dò trên byte 0..255 của ảnh, tức
                // hệ gamma. Project bật Linear color space thì Unity đã đổi màu sang
                // linear trước khi tới đây, chạy thẳng công thức lên đó ra màu khác hẳn.
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif

                rgb = AdjustColor(rgb, s, k, b);

                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
                #endif

                fixed alpha = silhouette * IN.color.a;

                // Blend One OneMinusSrcAlpha: trả về màu đã nhân sẵn alpha.
                return fixed4(rgb * alpha, alpha);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
