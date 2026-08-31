// Kẻ viền quanh từng ô CÓ MÀU, tính thẳng trong fragment shader thay vì nướng sẵn
// vào một texture cỡ cả bảng.
//
// Đầu vào duy nhất là MASK: mỗi texel đúng một ô, R = 1 nghĩa là ô đó có màu.
// Bảng 72x71 ra mask 74x73 byte, tức 5 KB — bản nướng sẵn cũ tốn 16 MB cho cùng
// chừng ấy thông tin, vì mỗi texel 4 byte mà chỉ mang một bit.
//
// Ba thứ có được nhờ tính tại chỗ:
//
//   1. Bề dày nét đo bằng PIXEL MÀN HÌNH, không phải texel. fwidth cho biết một
//      pixel màn hình đáng bao nhiêu ô, nên nét giữ đúng bề dày ở mọi mức zoom.
//      Bản nướng sẵn thì bề dày cố định theo texel: zoom ra là texture bị thu nhỏ,
//      lọc Point bỏ rơi nguyên đường kẻ, chỗ có chỗ không.
//
//   2. Khử răng cưa miễn phí. Độ phủ tính theo khoảng cách nên mép nét mượt, không
//      rung khi kéo bảng.
//
//   3. Mỗi RANH GIỚI được kẻ đúng một lần và nằm CHÍNH GIỮA ranh giới đó — điều
//      kiện là "ít nhất một trong hai ô kề nó có màu". Không có chuyện đường trong
//      lòng hình dày gấp đôi đường ở rìa.
//
// Mask có VIỀN ĐỆM một ô rỗng quanh bảng, và sprite phủ trọn cả phần đệm. Không có
// nó thì nửa ngoài của nét ở rìa bảng bị cắt mất, và rìa lại mảnh bằng nửa.
//
// wrapMode phải là Clamp: ở mép mask, ô hàng xóm lấy về chính nó, nên điều kiện
// "một trong hai ô có màu" vẫn ra đúng kết quả.
Shader "JewelPainter/Board Grid Lines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask ô (R = có màu)", 2D) = "black" {}

        _LineWidthPixels ("Bề dày nét (pixel màn hình)", Range(0.5, 8)) = 1.5
        _EdgeSoftness ("Độ mềm mép (pixel)", Range(0, 3)) = 1

        // Chiều cao màn hình mà Line Width Pixels được canh theo. Máy phân giải cao hơn
        // thì nét dày lên cùng tỉ lệ, nên nét trông bằng nhau trên mọi máy.
        // Để 0 thì bề dày tính đúng bằng pixel vật lý.
        _ReferenceScreenHeight ("Chiều cao màn hình gốc", Float) = 1080

        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
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
            #pragma target 3.0
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _LineWidthPixels;
            float _EdgeSoftness;
            float _ReferenceScreenHeight;

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

                return OUT;
            }

            // Độ phủ của một nét nằm giữa ranh giới, tính theo khoảng cách ĐÃ QUY RA
            // pixel màn hình. Cộng 0.5 để nét mảnh hơn một pixel vẫn còn hiện ra dưới
            // dạng nhạt màu thay vì biến mất — đúng chỗ mà bản nướng sẵn chịu thua.
            float Coverage(float distancePixels, float halfWidthPixels, float softness)
            {
                return saturate((halfWidthPixels - distancePixels) / softness + 0.5);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 texel     = _MainTex_TexelSize.xy;   // 1/W, 1/H
                float2 cellCount = _MainTex_TexelSize.zw;   // W, H — một texel là một ô

                float2 cell = IN.texcoord * cellCount;
                float2 f    = frac(cell);

                // fwidth(cell) = một pixel màn hình đáng bao nhiêu ô. Chia cho nó là đổi
                // khoảng cách từ đơn vị "ô" sang đơn vị "pixel màn hình".
                float2 cellsPerPixel = max(fwidth(cell), 1e-5);

                // Lấy mẫu ở TÂM ô chứ không phải ngay dưới chân pixel: sát ranh giới thì
                // uv +- texel dễ rơi nhầm sang ô kế bên.
                float2 center = (floor(cell) + 0.5) * texel;

                float m  = tex2D(_MainTex, center).r;
                float mL = tex2D(_MainTex, center + float2(-texel.x, 0)).r;
                float mR = tex2D(_MainTex, center + float2( texel.x, 0)).r;
                float mD = tex2D(_MainTex, center + float2(0, -texel.y)).r;
                float mU = tex2D(_MainTex, center + float2(0,  texel.y)).r;

                float scale = _ReferenceScreenHeight > 0.5
                    ? _ScreenParams.y / _ReferenceScreenHeight
                    : 1.0;

                float halfWidth = _LineWidthPixels * scale * 0.5;
                float softness  = max(_EdgeSoftness, 1e-3);

                float2 distanceToLow  = f / cellsPerPixel;
                float2 distanceToHigh = (1.0 - f) / cellsPerPixel;

                float a = 0;
                a = max(a, step(0.5, m + mL) * Coverage(distanceToLow.x,  halfWidth, softness));
                a = max(a, step(0.5, m + mR) * Coverage(distanceToHigh.x, halfWidth, softness));
                a = max(a, step(0.5, m + mD) * Coverage(distanceToLow.y,  halfWidth, softness));
                a = max(a, step(0.5, m + mU) * Coverage(distanceToHigh.y, halfWidth, softness));

                fixed4 c = IN.color;
                c.a *= a;

                clip(c.a - 0.002);

                // Blend One OneMinusSrcAlpha: màu phải nhân sẵn alpha.
                c.rgb *= c.a;

                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
