// Akşam sel — dinamik su katmanı (FloodWaterField). Haritanın tamamını kaplayan tek sprite üzerinde çizilir:
// her piksel dünya konumundan izometrik ızgara koordinatına çevrilir, FE_FloodData'dan dolma eşiği (R), akış yönü (G),
// akış hızı (B) ve maske (A) okunur. Derinlik = _Progress - eşik: negatifse ıslak zemin (koyulaşma + parıltı), pozitifse
// sığ → orta → derin su (sıralı dither ile piksel bantları), kenarda köpük çizgisi, akış yönünde kayan dalga tepeleri,
// yağmur yoğunluğuyla artan izometrik halka sıçramaları. Bütün hesaplar 1/64 birim (1 piksel) ızgarasına oturtulur.
// Universal2D geçişi sahnenin 2D ışıklarını (akşam global ışığı, sokak lambaları) uygular.
Shader "AfetUyanisi/FloodEvening/FloodWater2D"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _FloodData("Flood Data (R eşik, G yön, B hız, A maske)", 2D) = "black" {}
        _Progress("İlerleme", Range(0, 1)) = 0
        _Grid("Izgara (gx0, gy0, W, H)", Vector) = (-26, -16, 106, 60)
        _Root("Harita kökü (dünya x, y)", Vector) = (0, 0, 0, 0)
        _RainAmount("Yağmur halkaları", Range(0, 1)) = 0.3
        _DeepAt("Tam derinlik", Float) = 0.32
        _WetColor("Islak zemin", Color) = (0.05, 0.08, 0.15, 0.32)
        _ShallowColor("Sığ su", Color) = (0.42, 0.6, 0.78, 0.55)
        _MidColor("Orta su", Color) = (0.22, 0.43, 0.68, 0.76)
        _DeepColor("Derin su", Color) = (0.1, 0.22, 0.45, 0.88)
        _CrestColor("Dalga tepesi", Color) = (0.62, 0.8, 0.95, 0.85)
        _FoamColor("Köpük", Color) = (0.86, 0.95, 1, 0.92)
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_FloodData);
    SAMPLER(sampler_FloodData);

    CBUFFER_START(UnityPerMaterial)
        float _Progress;
        float4 _Grid;
        float4 _Root;
        float _RainAmount;
        float _DeepAt;
        half4 _WetColor, _ShallowColor, _MidColor, _DeepColor, _CrestColor, _FoamColor;
        half4 _Color;
    CBUFFER_END

    float FwHash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
    float FwNoise(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(FwHash(i), FwHash(i + float2(1, 0)), u.x), lerp(FwHash(i + float2(0, 1)), FwHash(i + float2(1, 1)), u.x), u.y);
    }
    static const float FwBayerM[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    float FwBayer(float2 pix)
    {
        int2 q = int2(fmod(abs(pix), 4.0));
        return (FwBayerM[q.y * 4 + q.x] + 0.5) / 16.0;
    }

    // Suyun rengi ve saydamlığı (ışıksız). a = 0 → bu pikselde su/ıslaklık yok.
    half4 FloodColor(float2 posWS)
    {
        float2 pix = floor(posWS * 64.0);
        float2 ws = (pix + 0.5) / 64.0;
        float2 rel = ws - _Root.xy;
        float gx = rel.x + 2.0 * rel.y;
        float gy = 2.0 * rel.y - rel.x;
        float2 uv = float2((gx - _Grid.x) / _Grid.z, (gy - _Grid.y) / _Grid.w);
        if (uv.x <= 0 || uv.y <= 0 || uv.x >= 1 || uv.y >= 1) return 0;
        half4 d = SAMPLE_TEXTURE2D(_FloodData, sampler_FloodData, uv);
        if (d.a < 0.5) return 0;
        float t = d.r > 0.995 ? 2.0 : d.r * (255.0 / 254.0);
        float time = _Time.y;
        float edgeN = (FwNoise(float2(gx, gy) * 3.1 + time * 0.12) - 0.5) * 0.02;
        float depth = _Progress - (t + edgeN);
        float bay = FwBayer(pix);

        if (depth < 0.0)
        {
            // Islak zemin: su gelmeden önce koyulaşır, arada parlak damla pikselleri
            float w = saturate(1.0 + depth / 0.07) * saturate(_Progress * 6.0);
            if (w <= bay) return 0;
            half4 c = _WetColor;
            if (FwHash(pix + floor(time * 1.5)) > 0.992) c = half4(_CrestColor.rgb, 0.45);
            return c;
        }

        float lv = saturate(depth / _DeepAt) + (bay - 0.5) * 0.14;
        half4 col = lv < 0.12 ? _ShallowColor : (lv < 0.5 ? _MidColor : _DeepColor);

        // Akış yönünde kayan dalga tepeleri (derinlik ve ilerlemeyle hızlanır)
        float ang = d.g * 6.2831853;
        float2 fdir = float2(cos(ang), sin(ang));
        float spd = d.b;
        float along = dot(float2(gx, gy), fdir), across = dot(float2(gx, gy), float2(-fdir.y, fdir.x));
        float flow = time * spd * (0.8 + 2.2 * _Progress) * (0.4 + saturate(depth * 4.0));
        float s = FwNoise(float2(along * 1.4 - flow, across * 4.5));
        float s2 = FwNoise(float2(along * 3.2 - flow * 1.7 + 17.0, across * 9.0));
        if (depth > 0.03 && s > 0.8 - 0.12 * spd * saturate(depth * 3.0)) col = _CrestColor;
        else if (depth > 0.12 && s2 > 0.86) col = lerp(col, _CrestColor, 0.6);

        // Yağmur halkaları (izometrik elips), yoğunluk yağmurla artar
        float cell = 0.42;
        float2 c0 = floor(ws / cell);
        float h = FwHash(c0 + 3.7);
        if (h < _RainAmount * 0.7)
        {
            float2 ctr = (c0 + 0.25 + 0.5 * float2(FwHash(c0 + 1.3), FwHash(c0 + 9.1))) * cell;
            float ph = frac(time * (0.7 + h) + h * 7.0);
            float r = ph * 0.15;
            float dist = length((ws - ctr) * float2(1.0, 2.0));
            if (abs(dist - r) < 0.011 && ph < 0.85) col = half4(_FoamColor.rgb, _FoamColor.a * (1.0 - ph));
        }

        // Kenar köpüğü (su çizgisi) — hafifçe nefes alır
        float foamW = 0.012 + 0.006 * sin(time * 2.0 + gx * 2.3 + gy * 1.7);
        if (depth < foamW) col = _FoamColor;
        return col;
    }
    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half2 lightingUV : TEXCOORD1; float2 posWS : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.posWS = TransformObjectToWorld(v.positionOS).xy;
                o.uv = v.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = FloodColor(i.posWS);
                c.a *= i.color.a;
                clip(c.a - 0.004);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(c.rgb, c.a, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 posWS : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.posWS = TransformObjectToWorld(v.positionOS).xy;
                o.color = v.color * _Color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = FloodColor(i.posWS);
                c.a *= i.color.a;
                clip(c.a - 0.004);
                return c;
            }
            ENDHLSL
        }
    }
}
