// Làm viên ngọc trông có khối và có bóng, thay vì là một mảng màu phẳng.
//
// GẮN Ở ĐÂU: material của PREFAB VIÊN NGỌC — cả `Jewel Prefab` của JewelLayer lẫn của
// JewelFlyEffect, để viên đang bay và viên đã đáp trông giống nhau. Mọi viên đều
// Instantiate từ prefab nên dùng chung một material: một shader phục vụ cả nghìn viên,
// và chúng vẫn batch chung.
//
// Gắn thêm lên lớp đã tô của BoardView cũng được: lúc zoom xa, ngọc bị cull đi và chỉ
// còn texture bảng. BoardView tự đẩy số ô vào _CellCount để một "ô" trong shader khớp
// với một ô thật của lưới.
//
// KHÔNG CÓ ĐỐM SÁNG. Đã thử cả chấm tròn lẫn vệt bầu dục, cả hai đều đọc ra như hình dán
// lên bề mặt: viên ngọc ở đây quá nhỏ trên màn hình để chứa nổi một điểm loé ra hồn, nên
// thứ duy nhất mắt thấy là một cái chấm có ranh giới rõ ràng.
//
// Độ sáng và độ bóng vì thế phải đến từ những cơ chế TRẢI RỘNG, không có biên:
//
//   1. Cạnh vát       — mép quay về phía sáng thì sáng, mép đối diện thì tối. Cho KHỐI.
//   2. Độ cong mặt ô  — dải chuyển sáng-tối trên toàn mặt. Cho mặt ô cong thay vì phẳng.
//   3. Hãm bên tối    — nghiêng cán cân về phía sáng. Cho ĐỘ SÁNG.
//   4. Bóng           — nửa hướng sáng nhạt về phía màu ánh sáng. Cho ĐỘ BÓNG.
//   5. Viền sáng      — vành sáng quanh cả mép. Đây là dấu hiệu mắt dùng để nhận ra thuỷ
//                       tinh và đá bóng: vật càng bóng thì rìa càng loé, ở MỌI phía chứ
//                       không riêng phía có đèn.
//   6. Khe tối        — vạch sẫm sát mép ngoài. Không tự làm gì sáng lên, nhưng tách các
//                       viên ra khỏi nhau, và độ sáng là thứ mắt đo bằng tương phản với
//                       xung quanh chứ không đo tuyệt đối.
//
// Không đọc thêm texture nào, mọi thứ tính từ toạ độ. Chi phí không phụ thuộc số viên.
Shader "JewelPainter/BoardGloss"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Huong anh sang)]
        _LightAngle ("Goc (do)", Range(0,360)) = 120

        [Header(Khoi)]
        _BevelStrength ("Do manh mep", Range(0,1.5)) = 0.55
        _BevelWidth ("Be day mep", Range(0.01,0.5)) = 0.22
        _BevelSharpness ("Do gay cua ranh gioi", Range(0,1)) = 0.75
        _FaceCurve ("Do cong mat o", Range(0,1)) = 0.28

        [Header(Do sang va do bong)]
        _ShadowDepth ("Do sau ben toi", Range(0,1)) = 0.45
        _GlossColor ("Mau anh sang", Color) = (1,1,1,1)
        _Gloss ("Do bong", Range(0,1)) = 0.38
        _RimLight ("Vien sang quanh mep", Range(0,1)) = 0.28
        _Vibrance ("Do ruc mau", Range(0,1)) = 0.22

        [Header(Tach vien)]
        _SeamDepth ("Do sam cua khe", Range(0,1)) = 0.35
        _SeamWidth ("Be rong khe", Range(0.005,0.2)) = 0.05

        [Header(Vet sang cheo tren ca bang)]
        _BandCenter ("Vi tri (don vi o)", Range(-40,40)) = 0
        _BandWidth ("Be rong (don vi o)", Range(0.5,60)) = 10
        _BandBoost ("Lam day them canh vat", Range(0,1.5)) = 0.25

        // Số ô mà một sprite trải qua. Viên ngọc là một ô nên để (1,1); BoardView tự
        // đẩy kích thước lưới vào khi shader này nằm trên lớp đã tô của bảng.
        [HideInInspector] _CellCount ("So o", Vector) = (1,1,0,0)
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

        Cull Off
        Lighting Off
        ZWrite Off
        // Giống Sprites/Default: đầu ra là màu ĐÃ NHÂN SẴN alpha.
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 worldXY  : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _CellCount;

            float _LightAngle;

            float _BevelStrength;
            float _BevelWidth;
            float _BevelSharpness;
            float _FaceCurve;

            float _ShadowDepth;
            fixed4 _GlossColor;
            float _Gloss;
            float _RimLight;
            float _Vibrance;

            float _SeamDepth;
            float _SeamWidth;

            float _BandCenter;
            float _BandWidth;
            float _BandBoost;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;

                // Vệt sáng phải bám theo BẢNG, không bám theo từng viên. Lấy toạ độ thế
                // giới thì mọi viên cùng nằm trên một vệt duy nhất; lấy UV của sprite thì
                // viên nào cũng có vệt riêng và vệt biến mất khỏi mắt người xem.
                o.worldXY = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Đổi độ "gãy" 0..1 thành bề rộng vùng chuyển tiếp của smoothstep.
            // 1 là cắt gọn như dao, 0 là tan mềm.
            float EdgeBlend(float sharpness)
            {
                return lerp(0.6, 0.02, saturate(sharpness));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                float2 cells = max(_CellCount.xy, 1.0);
                float2 cellUV = frac(i.uv * cells);

                float rad = radians(_LightAngle);
                float2 lightDir = float2(cos(rad), sin(rad));

                float2 fromCenter = cellUV - 0.5;

                // Khoảng cách tới mép gần nhất: 0 ở sát mép, 0.5 ở tâm ô.
                float toEdge = 0.5 - max(abs(fromCenter.x), abs(fromCenter.y));

                // --- 1. Cạnh vát ---
                float rim = 1.0 - smoothstep(0.0, max(_BevelWidth, 1e-4), toEdge);

                // Chia cho độ dài có cộng epsilon để tâm ô không chia cho 0.
                float2 dir = fromCenter / (length(fromCenter) + 1e-4);
                float blend = EdgeBlend(_BevelSharpness);
                float facing = smoothstep(-blend, blend, dot(dir, lightDir)) * 2.0 - 1.0;

                // --- 2. Độ cong mặt ô ---
                // Cạnh vát chỉ chạm tới viền, bỏ mặt ô phẳng lì ở giữa. Dải này trải trên
                // toàn mặt nên cả ô mới đọc ra là khối cong, không phải tấm phẳng có viền.
                float face = dot(fromCenter * 2.0, lightDir);   // -1 phía tối, +1 phía sáng

                // Vệt sáng chéo không tự vẽ gì, chỉ làm cạnh vát DÀY thêm ở dải nó quét
                // qua — đọc ra là ánh sáng rọi mạnh hơn ở vùng đó, không phải lớp phủ.
                float proj = dot(i.worldXY, lightDir);
                float band = saturate(1.0 - abs(proj - _BandCenter) / max(_BandWidth, 1e-4));
                band *= band;

                float shape = face * _FaceCurve
                            + rim * facing * _BevelStrength * (1.0 + band * _BandBoost);

                // --- 3. Hãm bên tối ---
                // shape đối xứng quanh 0: làm sáng một bên đúng bằng lượng làm tối bên kia,
                // nên trung bình bằng 0 — được khối nhưng KHÔNG thêm chút sáng nào. Hãm
                // riêng bên tối thì cán cân nghiêng về phía sáng và ô sáng lên thật.
                shape = shape >= 0.0 ? shape : shape * _ShadowDepth;

                // Nhân theo chính màu ô: ô sẫm được làm sáng/tối theo tỉ lệ của nó nên
                // không bị đẩy thành mảng trắng hay mảng đen bẹt.
                c.rgb += c.rgb * shape;

                // --- 4. Bóng ---
                // Nửa hướng sáng nhạt dần về phía màu ánh sáng. Đây mới là thứ tạo ra bóng:
                // một vùng SÁNG HƠN màu gốc, thứ mà cạnh vát về nguyên tắc không làm được.
                // Trải rộng cả nửa ô và không có biên nên không thể đọc ra thành hình dán.
                float lit = saturate(face);
                c.rgb = lerp(c.rgb, _GlossColor.rgb, lit * lit * _Gloss);

                // --- 5. Viền sáng ---
                // Sáng đều quanh CẢ mép, kể cả phía không có đèn. Nghe thì phản trực giác,
                // nhưng đó đúng là thứ phân biệt thuỷ tinh với nhựa: ở rìa, tia sáng lướt
                // gần như song song mặt vật nên phản xạ mạnh bất kể đèn nằm đâu.
                c.rgb = lerp(c.rgb, _GlossColor.rgb, rim * _RimLight);

                // --- Độ rực ---
                // Đẩy màu ra xa mức xám của chính nó. Ánh sáng ở trên làm màu nhạt đi;
                // bước này bù lại phần bão hoà đã mất, để ô sáng lên mà không bạc màu.
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(lum.xxx, c.rgb, 1.0 + _Vibrance);

                // --- 6. Khe tối ---
                // Vạch sẫm sát mép ngoài, đè lên cả viền sáng để hai viên kề nhau không
                // dính thành một mảng. Đặt cuối cùng vì nó phải thắng mọi thứ phía trên.
                float seam = 1.0 - smoothstep(0.0, max(_SeamWidth, 1e-4), toEdge);
                c.rgb *= 1.0 - seam * _SeamDepth;

                c.rgb = max(c.rgb, 0.0);

                // Nhân alpha ở bước cuối. Phần trong suốt của sprite tự triệt tiêu, nên
                // shader không cần biết đâu là hình viên ngọc đâu là nền.
                c.rgb *= c.a;

                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
