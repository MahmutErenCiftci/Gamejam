// Akşam sel — çevre tehlikesi geri bildirimi (FloodPlayerReaction): sprite'ın siluetini tek renkle çizer (ışıksız).
// Renk = SpriteRenderer.color (vertex rengi × unity_SpriteColor); şekil = sprite'ın alfa kanalı. Cenk'in üstüne çizilen kopya bununla
// elektrik çarpmasında mavi-beyaz, enkaz çarpmasında kırmızı kısa parlar; elektrik arkları da bununla (gece ışığından etkilenmez).
// Yalnız karakterin ve arkların piksellerini boyar: tam ekran parlama yoktur.
Shader "AfetUyanisi/FloodEvening/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        [HideInInspector] _Color("Tint", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
    CBUFFER_END

    struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
    struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

    Varyings Vert(Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS);
        o.color = v.color * unity_SpriteColor; // SpriteRenderer.color (URP 2D: unity_SpriteColor)
        o.uv = v.uv;
        return o;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
        return half4(i.color.rgb, i.color.a * step(0.5h, a));
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
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
