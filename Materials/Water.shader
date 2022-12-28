Shader "Custom/Water"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Alpha("Float",Float)=0.5
    }
    SubShader
    {
        Pass
        {
            Tags
            {
                "RenderType"="Opaque"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            fixed4 _Color;
            fixed _Alpha;
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 vert(fixed4 pos:POSITION):SV_POSITION
            {
                return UnityObjectToClipPos(pos);
            }

            fixed4 frag():SV_TARGET
            {
                return fixed4(_Color.r, _Color.g, _Color.b, _Alpha);
            }
            ENDCG
        }

    }
    Fallback ""
}