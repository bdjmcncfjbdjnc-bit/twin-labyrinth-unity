Shader "TwinLabyrinth/Room Sign"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct Glyph
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct ProjectedGlyph
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            ProjectedGlyph vert(Glyph glyph)
            {
                ProjectedGlyph result;
                result.position = UnityObjectToClipPos(glyph.position);
                result.uv = glyph.uv;
                result.color = glyph.color;
                return result;
            }

            fixed4 frag(ProjectedGlyph glyph) : SV_Target
            {
                fixed4 color = glyph.color;
                color.a *= tex2D(_MainTex, glyph.uv).a;
                return color;
            }
            ENDCG
        }
    }
}
