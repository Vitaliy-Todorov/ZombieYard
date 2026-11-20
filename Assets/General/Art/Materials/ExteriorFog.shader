Shader "Zombie Yard/Exterior Fog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE3D(_FogCells);
            float3 _FogOrigin, _FogDimensions;
            float _CellSize;
            float4 _MistColor;
            float _MistDensity;

            float4 Vert(float3 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position);
            }

            half4 Frag(float4 screen : SV_POSITION) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(screen.xy);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                float3 eye = GetCameraPositionWS();
                float3 ray = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP) - eye;
                float distance = max(length(ray), .0001);
                float3 direction = ray / distance;
                // Clip to the voxel volume and the opaque surface before traversing crossed cells.
                float3 dir = lerp(-1.0, 1.0, step(0.0, direction)) * max(abs(direction), .000001);
                float3 nearFace = (_FogOrigin - eye) / dir;
                float3 farFace = (_FogOrigin + _FogDimensions * _CellSize - eye) / dir;
                float3 lo = min(nearFace, farFace), hi = max(nearFace, farFace);
                float t = max(0, max(lo.x, max(lo.y, lo.z)));
                float stop = min(distance, min(hi.x, min(hi.y, hi.z)));
                if (stop <= t) return 0;

                float3 entry = (eye + direction * t - _FogOrigin) / _CellSize;
                int3 cell = clamp((int3)floor(entry + sign(dir) * .00001), 0, (int3)_FogDimensions - 1);
                int3 stride = (int3)sign(dir);
                float3 next = t + (cell + step(0.0, dir) - entry) * _CellSize / dir;
                float3 delta = _CellSize / abs(dir);
                float lengthInMist = 0;
                [loop] while (t < stop)
                {
                    float edge = min(next.x, min(next.y, next.z));
                    float end = min(stop, edge);
                    float fill = LOAD_TEXTURE3D(_FogCells, cell).r;
                    if (fill > 0)
                    {
                        float segment = max(0, end - t);
                        float surface = _FogOrigin.y + (cell.y + fill) * _CellSize;
                        float below = surface - (eye.y + t * direction.y);
                        if (abs(direction.y) < .000001) lengthInMist += below > 0 ? segment : 0;
                        else if (direction.y > 0) lengthInMist += clamp(below / direction.y, 0, segment);
                        else lengthInMist += segment - clamp(below / direction.y, 0, segment);
                    }
                    if (end >= stop || lengthInMist * _MistDensity > 6) break;
                    float3 crossed = step(next, edge);
                    cell += (int3)crossed * stride;
                    if (any(cell < 0) || any(cell >= (int3)_FogDimensions)) break;
                    next += crossed * delta;
                    t = edge;
                }
                return half4(_MistColor.rgb, 1 - exp(-_MistDensity * lengthInMist));
            }
            ENDHLSL
        }
    }
}

