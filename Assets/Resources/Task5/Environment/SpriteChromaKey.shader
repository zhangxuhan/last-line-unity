Shader "Task5/World/SpriteChromaKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                fixed high = max(color.r, max(color.g, color.b));
                fixed low = min(color.r, min(color.g, color.b));
                fixed neutral = high - low;
                fixed brightness = (color.r + color.g + color.b) / 3.0;
                fixed keyedAlpha = saturate((0.91 - brightness) * 18.0 + neutral * 9.0);
                color.a *= keyedAlpha;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
