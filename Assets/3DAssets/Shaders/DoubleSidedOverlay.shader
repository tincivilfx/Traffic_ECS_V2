Shader "CivilFX/DoubleSidedOverlay" {
		
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
        //_BumpMap ("Bump (RGB) Illumin (A)", 2D) = "bump" {}
    }
    SubShader {    
        //UsePass "Self-Illumin/VertexLit/BASE"
        //UsePass "Bumped Diffuse/PPL"
       
        // Ambient pass
        Pass {
        Name "BASE"
        Tags { "Queue" = "Overlay" "LightMode" = "Always" } // "RenderType" = "Opaque"
        Color [_PPLAmbient]
		Blend SrcAlpha OneMinusSrcAlpha
        SetTexture [_BumpMap] {
            constantColor (.5,.5,.5)
            combine constant lerp (texture) previous
            }
        SetTexture [_MainTex] {
            constantColor [_Color]
            Combine texture * previous DOUBLE, texture*constant
            }
        }
   
    // Vertex lights
    Pass {
        Name "BASE"
		Tags{ "Queue" = "Overlay" "LightMode" = "Vertex" }// "RenderType" = "Opaque" "Queue" = "Overlay+10" }
        Material {
            Diffuse [_Color]
            Emission [_PPLAmbient]
            Shininess [_Shininess]
            Specular [_SpecColor]
            }
        SeparateSpecular On
        Lighting On
        Cull Off

		Blend SrcAlpha OneMinusSrcAlpha
        SetTexture [_BumpMap] {
            constantColor (.5,.5,.5)
            combine constant lerp (texture) previous
            }
        SetTexture [_MainTex] {
            Combine texture * previous DOUBLE, texture*primary
            }
        }
    }
    FallBack "Diffuse", 1
}