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

        [Header(Vien ngoc ve theo tung o)]
        // Hình viên ngọc, lấy mẫu MỘT LẦN CHO MỖI Ô. Nhờ nó mà cả bảng chỉ còn một quad
        // thay vì mỗi ô một SpriteRenderer.
        //   R = bóng đổ (nhân với màu ô)   G = ánh loé (cộng vào)   A = hình viên
        // Để trắng trơn thì ô vẫn là hình vuông đặc như trước.
        _BeadTex ("Hinh vien ngoc", 2D) = "white" {}
        _BeadAmount ("Do manh", Range(0,1)) = 1

        [Header(Huong anh sang)]
        _LightAngle ("Goc (do)", Range(0,360)) = 120

        [Header(Khoi)]
        _BevelStrength ("Do manh mep", Range(0,1.5)) = 0.7
        _BevelWidth ("Be day mep", Range(0.01,0.5)) = 0.24
        _BevelSharpness ("Do gay cua ranh gioi", Range(0,1)) = 0.75
        _FaceCurve ("Do cong mat o", Range(0,1)) = 0.28

        // Bao nhiêu phần của vành là mặt tường thẳng đứng. Xem chú thích ở hàm frag.
        //
        // KHÔNG dùng [Tooltip] ở đây: ShaderLab không có drawer nào tên đó, mà dấu nháy
        // kép trong đối số còn làm hỏng luôn phép phân tích cả khối Properties — shader
        // hỏng thì Unity vẽ material bằng màu hồng. Muốn chú thích thì viết bằng dấu //.
        _WallShare ("Be rong mat tuong", Range(0,0.9)) = 0.45

        [Header(Do sang va do bong)]
        _ShadowDepth ("Do sau ben toi", Range(0,1)) = 0.45
        _GlossColor ("Mau anh sang", Color) = (1,1,1,1)
        _Gloss ("Do bong", Range(0,1)) = 0.38
        _RimLight ("Vien sang quanh mep", Range(0,1)) = 0.28
        _Vibrance ("Do ruc mau", Range(0,1)) = 0.22

        [Header(Tach vien va bong do)]
        _SeamDepth ("Do sam cua khe", Range(0,1)) = 0.35
        _SeamWidth ("Be rong khe", Range(0.005,0.2)) = 0.06
        _SeamShadowSide ("Do lech ve phia khuat sang", Range(0,1)) = 0.7

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
            sampler2D _BeadTex;
            float _BeadAmount;
            fixed4 _Color;
            float4 _CellCount;

            float _LightAngle;

            float _BevelStrength;
            float _BevelWidth;
            float _BevelSharpness;
            float _FaceCurve;
            float _WallShare;

            float _ShadowDepth;
            fixed4 _GlossColor;
            float _Gloss;
            float _RimLight;
            float _Vibrance;

            float _SeamDepth;
            float _SeamWidth;
            float _SeamShadowSide;

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
                float2 cellCoord = i.uv * cells;
                float2 cellUV = frac(cellCoord);

                // --- 0. Hình viên ngọc ---
                // Lấy mẫu bằng tex2Dgrad với đạo hàm của cellCoord, KHÔNG để GPU tự tính
                // từ cellUV: frac nhảy từ 1 về 0 ở mỗi mép ô, nên đạo hàm tại đó vọt lên
                // rất lớn và GPU chọn mức mip thấp nhất — kết quả là một đường nhoè chạy
                // dọc mọi mép ô. cellCoord thì liên tục nên đạo hàm của nó luôn đúng.
                fixed4 bead = tex2Dgrad(_BeadTex, cellUV, ddx(cellCoord), ddy(cellCoord));

                // Ô chưa tô có alpha 0 nên phép nhân dưới đây giữ nguyên nó trong suốt —
                // hình viên ngọc chỉ hiện ở ô đã tô, không cần hỏi ô nào đã tô.
                c.rgb *= lerp(1.0, bead.r, _BeadAmount);
                c.a *= lerp(1.0, bead.a, _BeadAmount);

                float rad = radians(_LightAngle);
                float2 lightDir = float2(cos(rad), sin(rad));

                float2 fromCenter = cellUV - 0.5;

                // Khoảng cách tới mép gần nhất: 0 ở sát mép, 0.5 ở tâm ô.
                float toEdge = 0.5 - max(abs(fromCenter.x), abs(fromCenter.y));

                // --- 1. Cạnh vát ---
                // Vành chia làm hai phần: MẶT TƯỜNG sát mép giữ nguyên một độ sáng, rồi
                // mới có vai bo tròn nối vào mặt phẳng ở giữa.
                //
                // Đây là chỗ quyết định viên trông cao hay thấp. Dốc trơn từ mép vào tâm
                // chỉ có đúng một đường sáng nhất và một đường tối nhất, mảnh như sợi chỉ
                // — mắt đọc ra là mặt cong. Một DẢI cùng độ sáng thì đọc ra là một bức
                // tường phẳng dựng đứng, và tường có bề cao.
                //
                // Để 0 là quay về dốc trơn như cũ.
                float u = saturate(toEdge / max(_BevelWidth, 1e-4));
                float rim = 1.0 - smoothstep(saturate(_WallShare), 1.0, u);

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

                // Ánh loé vẽ sẵn trong ảnh viên ngọc. Cộng vào chứ không pha, vì nó đã
                // được vẽ đúng chỗ rồi — pha sẽ làm nó nhạt đi ở ô màu sáng.
                c.rgb += _GlossColor.rgb * bead.g * _BeadAmount;

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

                // --- 6. Khe tối và bóng đổ ---
                // Vạch sẫm sát mép ngoài, đè lên cả viền sáng để hai viên kề nhau không
                // dính thành một mảng. Đặt cuối cùng vì nó phải thắng mọi thứ phía trên.
                //
                // Vạch này KHÔNG đều quanh viên: phía khuất sáng sẫm hơn hẳn. Đó chính là
                // bóng viên ngọc đổ xuống mặt bảng, và bóng đổ là bằng chứng mạnh nhất cho
                // mắt biết một vật đang nhô lên khỏi bề mặt chứ không nằm phẳng trên đó.
                // Cạnh vát tả được hình dạng, nhưng chỉ bóng đổ mới tả được CHIỀU CAO.
                float seam = 1.0 - smoothstep(0.0, max(_SeamWidth, 1e-4), toEdge);

                // facing chạy từ -1 ở phía tối tới +1 ở phía sáng.
                float shadowSide = saturate(-facing);
                float seamDepth = _SeamDepth * (1.0 + _SeamShadowSide * (shadowSide * 2.0 - 1.0));

                c.rgb *= 1.0 - saturate(seam * seamDepth);

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
