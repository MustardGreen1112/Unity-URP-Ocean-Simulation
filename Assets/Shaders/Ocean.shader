Shader "Custom/Ocean"
{
    Properties
    {
        _MainTex                ("Texture",        2D)   = "white" {}
        [HideInInspector]_Amplitude_h ("Amplitude_h", 2D) = "black" {}
        [HideInInspector]_Amplitude_Dx("Amplitude_Dx",2D) = "black" {}
        [HideInInspector]_Amplitude_Dz("Amplitude_Dz",2D) = "black" {}
        [HideInInspector]_LengthScale("Length Scale", Float) = 1.0
        _Color_Shallow ("Shallow Color", Color) = (0.0, 0.5, 1.0, 1.0)
        _Color_Deep ("Deep Color", Color) = (0.0, 0.0, 0.5, 1.0)
        _DepthThreshold ("Depth Threshold", Float) = 10.0 

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // Core URP bits:
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"          // defines Attributes, Varyings, InitializeInputData
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"


            float _LengthScale;
            float _DepthThreshold;
            float4 _Color_Shallow;
            float4 _Color_Deep;
            TEXTURE2D(_Amplitude_h);   SAMPLER(sampler_Amplitude_h);
            TEXTURE2D(_Amplitude_Dx);  SAMPLER(sampler_Amplitude_Dx);
            TEXTURE2D(_Amplitude_Dz);  SAMPLER(sampler_Amplitude_Dz);

            struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}


            // vert/signature must use URP’s Attributes / Varyings:
            // Varyings vert(Attributes v)
            // {
            //     Varyings o = (Varyings)0;
            //     UNITY_SETUP_INSTANCE_ID(v);
            //     UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            // 
            //     VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
            //     half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
            //     // world position for your height‐map lookup
            //     float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
            //     o.positionWS = worldPos;
            // 
            //     // sample height
            //     float h = _Amplitude_h.SampleLevel(sampler_Amplitude_h, worldPos.xz/_LengthScale, 0).x;
            //     float3 offset = float3(0,h,0);
            // 
            //     // displace and compute clip‐pos
            //     float3 localDisplaced = v.positionOS.xyz + mul(unity_WorldToObject, float4(offset,1)).xyz;
            //     o.positionCS = TransformObjectToHClip(localDisplaced);
            // 
            //     // UV in TEXCOORD0
            //     o.uv = v.texcoord;
            // 
            //     return o;
            // }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
            
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
            
                // normalWS and tangentWS already normalize.
                // this is required to avoid skewing the direction during interpolation
                // also required for per-vertex lighting and SH evaluation
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                /////////////// Modify Height.///////////////////////
                float3 worldPos = vertexInput.positionWS;
                float h = _Amplitude_h.SampleLevel(sampler_Amplitude_h, worldPos.xz/_LengthScale, 0).x;
                float3 offset = float3(0,h,0);
                vertexInput.positionWS = mul(unity_ObjectToWorld, (input.positionOS + mul(unity_WorldToObject, float4(offset,1.0)))).xyz;
                vertexInput.positionCS = TransformWorldToHClip(vertexInput.positionWS);
                /////////////// End Modification ////////////////////

                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
            
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
            
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            
                // already normalized from normal transform to WS.
                output.normalWS = normalInput.normalWS;
            #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                real sign = input.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
            #endif
            #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                output.tangentWS = tangentWS;
            #endif
            
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
                output.viewDirTS = viewDirTS;
            #endif
            
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
            #else
                output.fogFactor = fogFactor;
            #endif
            
            #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
                output.positionWS = vertexInput.positionWS;
            #endif
            
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
            #endif

                output.positionCS = vertexInput.positionCS;

                return output;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // float3 n = normalize(float3(
                //     -_Amplitude_Dx.Sample(sampler_Amplitude_Dx, i.positionWS.xz/_LengthScale).x,
                //     1,
                //     -_Amplitude_Dz.Sample(sampler_Amplitude_Dz, i.positionWS.xz/_LengthScale).x
                // ));
                // rebuild normal
                float3 n = normalize(float3(
                    -_Amplitude_Dx.Sample(sampler_Amplitude_Dx, i.positionWS.xz/_LengthScale).x / (_Amplitude_Dx.Sample(sampler_Amplitude_Dz, i.positionWS.xz/_LengthScale).y + 1.0),
                    1.0,
                    -_Amplitude_Dz.Sample(sampler_Amplitude_Dz, i.positionWS.xz/_LengthScale).x / (_Amplitude_Dz.Sample(sampler_Amplitude_Dz, i.positionWS.xz/_LengthScale).y + 1.0)
                ));
                // n = float3(.0, 1.0, .0);
                // set up URP InputData
                InputData inputData;
                InitializeInputData(i, 0, inputData);
                inputData.normalWS = n;
                // InitializeBakedGIData (i, inputData);
                   
                // Transform world-space normal to tangent space
                // half3x3 tangentToWorld = inputData.tangentToWorld; Ensure tangentToWorld is initialized
                // half3 normalTS = TransformWorldToTangent(n, tangentToWorld);

                // build a trivial white surface
                SurfaceData surf = (SurfaceData)0;
                surf.albedo              = _Color_Deep;
                surf.specular            = 1.0;
                surf.metallic            = 0;
                surf.smoothness          = 0.9;
                surf.emission            = 0;
                surf.occlusion           = 1;
                surf.alpha               = 1;
                surf.clearCoatMask       = 0.0;
                surf.clearCoatSmoothness = 0.0;
                surf.normalTS            = 0;

                // do URP’s PBR
                float4 outCol = UniversalFragmentPBR(inputData, surf);
                float depth = i.positionWS.y;
                float t = saturate(depth / _DepthThreshold);
                outCol.rgb = lerp(outCol, _Color_Shallow, t);
                // float3 col = 0.5 * n + 0.5;
                // if(dot(n, GetWorldSpaceNormalizeViewDir(i.positionWS)) < 0){
                //     return float4(0,0,0,0.0);
                //     }
                // col.x = 0; col.z = 0;
                // float4 frag_col = float4(col, 1.0);
                // return frag_col;
                return outCol;
            }
            ENDHLSL
        }
    }
}