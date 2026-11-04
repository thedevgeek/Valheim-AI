using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace ValheimCompanion
{
    internal sealed class LucyAppearance : MonoBehaviour
    {
        private static readonly Color CoatColorMultiplier = new Color(0.82f, 0.82f, 0.82f, 1f);
        private static readonly Color CoatShadowFill = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color FacialMarkingColor = new Color(0.008f, 0.004f, 0.003f, 1f);
        private static readonly Color BodySpeckleColor = new Color(0.34f, 0.28f, 0.22f, 1f);
        private static readonly Color CollarColor = new Color(0.78f, 0.08f, 1f, 1f);
        private const float OverallSizeMultiplier = 0.80f;
        private const float LegLengthMultiplier = 0.55f;
        private const float BodyLengthMultiplier = 1.12f;
        private const float PawSizeMultiplier = 0.76f;
        private const float HeadLengthMultiplier = 0.90f;
        private const float HeadWidthMultiplier = 1.12f;
        private static readonly string[][] LegChains =
        {
            new[] { "L UpperArm", "L Forearm", "L Hand" },
            new[] { "R UpperArm", "R Forearm", "R Hand" },
            new[] { "L Thigh", "L Calf", "L HorseLink", "L Foot" },
            new[] { "R Thigh", "R Calf", "R HorseLink", "R Foot" }
        };
        private static readonly HashSet<string> UvDiagnosticBoneNames = new HashSet<string>
        {
            "Head", "Eye L", "Eye R", "L Ear", "R Ear"
        };
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Material> _coatMaterials = new List<Material>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly List<GameObject> _facialMarkings = new List<GameObject>();
        private readonly List<GameObject> _accessories = new List<GameObject>();
        private readonly List<Mesh> _markingMeshes = new List<Mesh>();
        private readonly List<Mesh> _accessoryMeshes = new List<Mesh>();
        private readonly HashSet<Transform> _shortenedBones = new HashSet<Transform>();
        private readonly HashSet<Transform> _lengthenedBones = new HashSet<Transform>();
        private readonly HashSet<Transform> _reshapedBones = new HashSet<Transform>();
        private readonly HashSet<Material> _markedMaterials = new HashSet<Material>();
        private bool _rigLogged;
        private bool _overallSizeApplied;

        public static bool Apply(Tameable companion, ManualLogSource logger)
        {
            var character = companion.GetComponent<Character>();
            if (character != null)
            {
                character.m_name = "Lucy";
            }

            var existingAppearance = companion.GetComponent<LucyAppearance>();
            if (existingAppearance != null)
            {
                existingAppearance.ApplyOverallSize(logger);
                existingAppearance.ApplyNeckCollar(logger);
                existingAppearance.SetNeutralWhite();
                existingAppearance.ApplyShortLegProportions(logger);
                existingAppearance.LogRigDetails(logger);
                return true;
            }

            var renderers = companion.GetComponentsInChildren<Renderer>(true);
            var skinnedRenderers = companion.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderers.Length == 0)
            {
                logger.LogError("Cannot style Lucy because the vanilla companion has no skinned model renderers.");
                return false;
            }

            var appearance = companion.gameObject.AddComponent<LucyAppearance>();
            var styledMaterials = 0;
            var materialNames = new HashSet<string>();
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            foreach (var renderer in skinnedRenderers)
            {
                var sourceMaterials = renderer.sharedMaterials;
                var instanceMaterials = new Material[sourceMaterials.Length];

                for (var index = 0; index < sourceMaterials.Length; index++)
                {
                    var source = sourceMaterials[index];
                    if (source == null)
                    {
                        continue;
                    }

                    var material = new Material(source)
                    {
                        name = source.name + " (Lucy)"
                    };
                    SetNeutralWhite(material);
                    appearance.ApplyFacialMarking(renderer, material, logger);

                    instanceMaterials[index] = material;
                    appearance._materials.Add(material);
                    appearance._coatMaterials.Add(material);
                    materialNames.Add(source.name);
                    styledMaterials++;
                }

                renderer.materials = instanceMaterials;
            }

            if (styledMaterials == 0)
            {
                logger.LogError("Cannot style Lucy because the wolf model has no valid materials.");
                UnityEngine.Object.Destroy(appearance);
                return false;
            }

            logger.LogInfo(
                $"Applied Lucy's brighter neutral-white coat using the animated vanilla canine model. " +
                $"Preserved original textures and gait; coat color multiplier is {CoatColorMultiplier.r:0.0}x. " +
                $"Model material(s): {string.Join(", ", materialNames)}.");
            appearance.ApplyOverallSize(logger);
            appearance.ApplyShortLegProportions(logger);
            appearance.ApplyNeckCollar(logger);
            appearance.LogRigDetails(logger);
            return true;
        }

        private void ApplyOverallSize(ManualLogSource logger)
        {
            if (_overallSizeApplied)
            {
                return;
            }

            transform.localScale *= OverallSizeMultiplier;
            _overallSizeApplied = true;
            logger.LogInfo($"Set Lucy's overall size to {OverallSizeMultiplier:0.00}x her original size.");
        }

        private void ApplyNeckCollar(ManualLogSource logger)
        {
            if (_accessories.Count > 0)
            {
                return;
            }

            SkinnedMeshRenderer? sourceRenderer = null;
            Transform? neck = null;
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                neck = FindBone(renderer, "Neck");
                if (neck != null)
                {
                    sourceRenderer = renderer;
                    break;
                }
            }

            if (sourceRenderer == null || neck == null)
            {
                logger.LogError("Cannot create Lucy's purple collar because the wolf neck bone was not found.");
                return;
            }

            var sourceMaterial = sourceRenderer.sharedMaterial;
            if (sourceMaterial == null)
            {
                logger.LogError("Cannot create Lucy's purple collar because the wolf skin material was not found.");
                return;
            }

            var collarObject = new GameObject("Lucy purple collar");
            collarObject.transform.SetParent(neck, false);
            var head = FindBone(sourceRenderer, "Head");
            var neckToHead = head != null ? neck.InverseTransformPoint(head.position) : Vector3.zero;
            var collarPosition = neckToHead.sqrMagnitude > 0.0001f
                ? neckToHead * 0.55f
                : new Vector3(-0.11f, 0f, 0f);
            collarObject.transform.localPosition = collarPosition;
            if (neckToHead.sqrMagnitude > 0.0001f)
            {
                collarObject.transform.localRotation = Quaternion.FromToRotation(Vector3.right, neckToHead);
            }

            collarObject.transform.localScale = GetBoneCompensatedScale(neck);
            var meshFilter = collarObject.AddComponent<MeshFilter>();
            var collarMesh = CreateCollarMesh();
            collarMesh.name = "Lucy purple collar band";
            meshFilter.sharedMesh = collarMesh;
            var collarRenderer = collarObject.AddComponent<MeshRenderer>();
            var collarMaterial = new Material(sourceMaterial)
            {
                name = "Lucy purple collar material",
                color = CollarColor
            };
            if (collarMaterial.HasProperty("_Color"))
            {
                collarMaterial.SetColor("_Color", CollarColor);
            }

            if (collarMaterial.HasProperty("_EmissionColor"))
            {
                collarMaterial.SetColor("_EmissionColor", CollarColor * 0.65f);
                collarMaterial.EnableKeyword("_EMISSION");
            }

            var solidTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Lucy purple collar texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            solidTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            solidTexture.Apply();
            collarMaterial.mainTexture = solidTexture;
            collarRenderer.sharedMaterial = collarMaterial;
            _materials.Add(collarMaterial);
            _textures.Add(solidTexture);
            _accessories.Add(collarObject);
            _accessoryMeshes.Add(collarMesh);
            logger.LogInfo(
                $"Added Lucy's purple collar using Valheim's creature shader, attached to her animated neck; " +
                $"localPosition={collarPosition}; localScale={collarObject.transform.localScale}; " +
                $"neckScale={neck.lossyScale}.");
        }

        private static Mesh CreateCollarMesh()
        {
            const int segments = 48;
            const float halfWidth = 0.05f;
            const float outerRadius = 0.18f;
            const float innerRadius = 0.14f;
            var vertices = new Vector3[segments * 4];
            var triangles = new int[segments * 24];

            for (var index = 0; index < segments; index++)
            {
                var angle = 2f * Mathf.PI * index / segments;
                var y = Mathf.Cos(angle);
                var z = Mathf.Sin(angle);
                vertices[index * 4] = new Vector3(-halfWidth, y * outerRadius, z * outerRadius);
                vertices[index * 4 + 1] = new Vector3(halfWidth, y * outerRadius, z * outerRadius);
                vertices[index * 4 + 2] = new Vector3(-halfWidth, y * innerRadius, z * innerRadius);
                vertices[index * 4 + 3] = new Vector3(halfWidth, y * innerRadius, z * innerRadius);
            }

            var triangleIndex = 0;
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                var outerFront = index * 4;
                var outerBack = outerFront + 1;
                var innerFront = outerFront + 2;
                var innerBack = outerFront + 3;
                var nextOuterFront = next * 4;
                var nextOuterBack = nextOuterFront + 1;
                var nextInnerFront = nextOuterFront + 2;
                var nextInnerBack = nextOuterFront + 3;

                AddQuad(triangles, ref triangleIndex, outerFront, nextOuterFront, nextOuterBack, outerBack);
                AddQuad(triangles, ref triangleIndex, innerFront, innerBack, nextInnerBack, nextInnerFront);
                AddQuad(triangles, ref triangleIndex, outerFront, outerBack, innerBack, innerFront);
                AddQuad(triangles, ref triangleIndex, outerBack, nextOuterBack, nextInnerBack, innerBack);
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(int[] triangles, ref int index, int a, int b, int c, int d)
        {
            triangles[index++] = a;
            triangles[index++] = b;
            triangles[index++] = c;
            triangles[index++] = a;
            triangles[index++] = c;
            triangles[index++] = d;
        }

        private void ApplyShortLegProportions(ManualLogSource logger)
        {
            var changedBones = new List<string>();
            var stretchedBones = new List<string>();
            var reshapedBones = new List<string>();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var head = FindBone(renderer, "Head");
                if (head != null && _reshapedBones.Add(head))
                {
                    var scale = head.localScale;
                    head.localScale = new Vector3(
                        scale.x * HeadLengthMultiplier,
                        scale.y,
                        scale.z * HeadWidthMultiplier);
                    reshapedBones.Add(head.name);
                }

                foreach (var boneName in new[] { "Spine", "Spine1", "Spine2", "Neck" })
                {
                    var bone = FindBone(renderer, boneName);
                    if (bone == null || !_lengthenedBones.Add(bone))
                    {
                        continue;
                    }

                    var scale = bone.localScale;
                    var segmentScale = Mathf.Pow(BodyLengthMultiplier, 0.25f);
                    bone.localScale = new Vector3(scale.x * segmentScale, scale.y, scale.z);
                    stretchedBones.Add(bone.name);
                }

                foreach (var chain in LegChains)
                {
                    var chainScale = Mathf.Pow(LegLengthMultiplier, 1f / chain.Length);
                    foreach (var boneName in chain)
                    {
                        var bone = FindBone(renderer, boneName);
                        if (bone == null || !_shortenedBones.Add(bone))
                        {
                            continue;
                        }

                        var scale = bone.localScale;
                        bone.localScale = new Vector3(scale.x * chainScale, scale.y, scale.z);
                        if (boneName == "L Hand" || boneName == "R Hand"
                            || boneName == "L Foot" || boneName == "R Foot")
                        {
                            bone.localScale = new Vector3(
                                bone.localScale.x,
                                bone.localScale.y * PawSizeMultiplier,
                                bone.localScale.z * PawSizeMultiplier);
                        }

                        changedBones.Add($"{bone.name} ({scale.x:0.00}->{bone.localScale.x:0.00})");
                    }
                }
            }

            if (changedBones.Count > 0)
            {
                logger.LogInfo(
                    $"Applied matched front/rear short-leg proportions ({LegLengthMultiplier:0.00}x total chain length), " +
                    $"body length {BodyLengthMultiplier:0.00}x on [{string.Join(", ", stretchedBones)}], " +
                    $"compact broad head {HeadLengthMultiplier:0.00}x long/{HeadWidthMultiplier:0.00}x wide " +
                    $"on [{string.Join(", ", reshapedBones)}], " +
                    $"and {PawSizeMultiplier:0.00}x paw scale: " +
                    $"{string.Join(", ", changedBones)}. Native wolf animation remains enabled.");
            }
            else if (_shortenedBones.Count == 0 || _lengthenedBones.Count == 0)
            {
                logger.LogWarning(
                    $"Lucy proportion adjustment may be incomplete: legBones={_shortenedBones.Count}; " +
                    $"bodyBones={_lengthenedBones.Count}.");
            }
        }

        private void LogRigDetails(ManualLogSource logger)
        {
            if (_rigLogged)
            {
                return;
            }

            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                var boneDetails = new List<string>();
                foreach (var bone in renderer.bones)
                {
                    if (bone == null)
                    {
                        continue;
                    }

                    boneDetails.Add(
                        $"{bone.name}@{bone.localPosition.x:0.00},{bone.localPosition.y:0.00},{bone.localPosition.z:0.00}");
                }

                logger.LogInfo(
                    $"Lucy rig diagnostic: renderer={renderer.name}; mesh={mesh?.name ?? "none"}; " +
                    $"rootBone={renderer.rootBone?.name ?? "none"}; " +
                    $"bounds={mesh?.bounds.size.ToString("F2") ?? "none"}; bones=[{string.Join("; ", boneDetails)}].");

                LogUvBounds(renderer, logger);

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    var texture = material.mainTexture;
                    logger.LogInfo(
                        $"Lucy material diagnostic: material={material.name}; shader={material.shader?.name ?? "none"}; " +
                        $"mainTexture={texture?.name ?? "none"}; size={texture?.width ?? 0}x{texture?.height ?? 0}; " +
                        $"colorProperty={material.HasProperty("_Color")}; mainTextureProperty={material.HasProperty("_MainTex")}.");
                }
            }

            _rigLogged = true;
        }

        private void ApplyFacialMarking(
            SkinnedMeshRenderer renderer,
            Material material,
            ManualLogSource logger)
        {
            if (!_markedMaterials.Add(material))
            {
                return;
            }

            var sourceTexture = material.mainTexture as Texture2D;
            var mesh = renderer.sharedMesh;
            if (sourceTexture == null || mesh == null)
            {
                logger.LogError(
                    $"Cannot apply Lucy's facial marking: renderer={renderer.name}; " +
                    $"texture={sourceTexture != null}; mesh={mesh != null}.");
                return;
            }

            var markedTexture = CopyTexture(sourceTexture, logger);
            if (markedTexture == null)
            {
                return;
            }

            var speckles = PaintCoatSpeckles(mesh, renderer, markedTexture);
            if (speckles == 0)
            {
                logger.LogWarning("Lucy body speckles were not applied because no suitable torso/face vertices were found.");
            }
            markedTexture.Apply();
            material.mainTexture = markedTexture;
            _textures.Add(markedTexture);

            var eye = FindBone(renderer, "Eye R");
            var head = FindBone(renderer, "Head");
            var patchCreated = eye != null && head != null && CreateFacialMarking(eye, head, material, logger);
            if (!patchCreated)
            {
                logger.LogError(
                    $"Lucy one-eye marking was not created: rightEye={eye != null}; head={head != null}.");
            }

            logger.LogInfo(
                $"Applied Lucy's single-eye eye-bone patch={patchCreated} " +
                $"and {speckles} face/body coat speckles.");
        }

        private bool CreateFacialMarking(
            Transform eye,
            Transform head,
            Material coatMaterial,
            ManualLogSource logger)
        {
            const int segments = 20;
            const float radiusX = 0.085f;
            const float radiusY = 0.075f;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            var eyePosition = head.InverseTransformPoint(eye.position);
            var outward = eyePosition.z < 0f ? -1f : 1f;
            var offset = eyePosition + new Vector3(0f, 0f, outward * 0.025f);
            vertices[0] = offset;
            for (var index = 0; index < segments; index++)
            {
                var angle = 2f * Mathf.PI * index / segments;
                var xShape = 1f + 0.12f * Mathf.Cos(angle);
                vertices[index + 1] = offset + new Vector3(
                    -0.012f + Mathf.Cos(angle) * radiusX * xShape,
                    Mathf.Sin(angle) * radiusY,
                    0f);

                var next = (index + 1) % segments;
                var triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = outward > 0f ? index + 1 : next + 1;
                triangles[triangleIndex + 2] = outward > 0f ? next + 1 : index + 1;
            }

            var markingMesh = new Mesh
            {
                name = "Lucy right-eye patch",
                vertices = vertices,
                triangles = triangles
            };
            markingMesh.RecalculateNormals();
            markingMesh.RecalculateBounds();

            var markingObject = new GameObject("Lucy right-eye patch");
            markingObject.transform.SetParent(head, false);
            markingObject.transform.localScale = GetBoneCompensatedScale(head);
            var markingFilter = markingObject.AddComponent<MeshFilter>();
            markingFilter.sharedMesh = markingMesh;
            var markingRenderer = markingObject.AddComponent<MeshRenderer>();
            var markingMaterial = new Material(coatMaterial)
            {
                name = "Lucy single-eye patch",
                color = FacialMarkingColor
            };
            var solidTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Lucy eye patch solid texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            solidTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            solidTexture.Apply();
            markingMaterial.mainTexture = solidTexture;
            _textures.Add(solidTexture);
            if (markingMaterial.HasProperty("_Color"))
            {
                markingMaterial.SetColor("_Color", FacialMarkingColor);
            }

            markingRenderer.sharedMaterial = markingMaterial;
            _materials.Add(markingMaterial);
            _markingMeshes.Add(markingMesh);
            _facialMarkings.Add(markingObject);
            logger.LogInfo(
                $"Created a head-attached right-side eye patch with {segments} curved segments; " +
                $"localPosition={offset}; localScale={markingObject.transform.localScale}; " +
                $"outward={outward:0}.");
            return true;
        }

        private Vector3 GetBoneCompensatedScale(Transform bone)
        {
            var rootScale = transform.lossyScale;
            var boneScale = bone.lossyScale;
            return new Vector3(
                boneScale.x != 0f ? rootScale.x / boneScale.x : 1f,
                boneScale.y != 0f ? rootScale.y / boneScale.y : 1f,
                boneScale.z != 0f ? rootScale.z / boneScale.z : 1f);
        }

        private static Texture2D? CopyTexture(Texture2D sourceTexture, ManualLogSource logger)
        {
            var previousActive = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Texture2D? texture = null;
            try
            {
                Graphics.Blit(sourceTexture, temporary);
                RenderTexture.active = temporary;
                texture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false, false)
                {
                    name = sourceTexture.name + " (Lucy speckles)",
                    filterMode = sourceTexture.filterMode,
                    wrapMode = sourceTexture.wrapMode
                };
                texture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
                texture.Apply();
                return texture;
            }
            catch (UnityException exception)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                logger.LogError($"Cannot copy Lucy's coat texture for speckles: {exception.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static int PaintCoatSpeckles(Mesh mesh, SkinnedMeshRenderer renderer, Texture2D texture)
        {
            var uvs = mesh.uv;
            var weights = mesh.boneWeights;
            var bones = renderer.bones;
            if (uvs.Length == 0 || weights.Length != uvs.Length)
            {
                return 0;
            }

            var pixels = texture.GetPixels();
            var count = 0;
            for (var vertexIndex = 0; vertexIndex < uvs.Length; vertexIndex++)
            {
                var weight = weights[vertexIndex];
                var dominantIndex = weight.boneIndex0;
                var dominantWeight = weight.weight0;
                if (weight.weight1 > dominantWeight)
                {
                    dominantIndex = weight.boneIndex1;
                    dominantWeight = weight.weight1;
                }

                if (weight.weight2 > dominantWeight)
                {
                    dominantIndex = weight.boneIndex2;
                    dominantWeight = weight.weight2;
                }

                if (weight.weight3 > dominantWeight)
                {
                    dominantIndex = weight.boneIndex3;
                    dominantWeight = weight.weight3;
                }

                if (dominantWeight <= 0f || dominantIndex < 0 || dominantIndex >= bones.Length)
                {
                    continue;
                }

                var boneName = bones[dominantIndex]?.name;
                var isFace = boneName == "Head" || boneName == "L Cheek" || boneName == "R Cheek";
                var isBody = boneName == "Spine" || boneName == "Spine1" || boneName == "Spine2";
                if ((!isFace && !isBody) || (vertexIndex * 73 + 19) % 17 >= (isFace ? 9 : 6))
                {
                    continue;
                }

                var uv = uvs[vertexIndex];
                uv.x = Mathf.Repeat(uv.x, 1f);
                uv.y = Mathf.Repeat(uv.y, 1f);
                var centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * texture.width), 0, texture.width - 1);
                var centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * texture.height), 0, texture.height - 1);
                var radius = 2 + (vertexIndex % 3);
                for (var y = -radius; y <= radius; y++)
                {
                    for (var x = -radius; x <= radius; x++)
                    {
                        var distance = Mathf.Sqrt(x * x + y * y) / radius;
                        if (distance > 1f)
                        {
                            continue;
                        }

                        var pixelX = centerX + x;
                        var pixelY = centerY + y;
                        if (pixelX < 0 || pixelX >= texture.width || pixelY < 0 || pixelY >= texture.height)
                        {
                            continue;
                        }

                        var opacity = (1f - Mathf.SmoothStep(0.55f, 1f, distance)) * 0.92f;
                        var pixelIndex = pixelY * texture.width + pixelX;
                        pixels[pixelIndex] = Color.Lerp(pixels[pixelIndex], BodySpeckleColor, opacity);
                    }
                }

                count++;
            }

            texture.SetPixels(pixels);
            return count;
        }

        private static Transform? FindBone(SkinnedMeshRenderer renderer, string boneName)
        {
            foreach (var bone in renderer.bones)
            {
                if (bone != null && bone.name == boneName)
                {
                    return bone;
                }
            }

            return null;
        }

        private static void LogUvBounds(SkinnedMeshRenderer renderer, ManualLogSource logger)
        {
            var mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            var uvs = mesh.uv;
            var weights = mesh.boneWeights;
            if (uvs.Length == 0 || weights.Length != uvs.Length)
            {
                logger.LogWarning(
                    $"Lucy UV diagnostic unavailable for renderer={renderer.name}: " +
                    $"uvCount={uvs.Length}; boneWeightCount={weights.Length}.");
                return;
            }

            var bounds = new Dictionary<string, Vector2[]>();
            var vertexCounts = new Dictionary<string, int>();
            var bones = renderer.bones;
            for (var vertexIndex = 0; vertexIndex < uvs.Length; vertexIndex++)
            {
                var weight = weights[vertexIndex];
                var dominantBoneIndex = weight.boneIndex0;
                var dominantWeight = weight.weight0;
                if (weight.weight1 > dominantWeight)
                {
                    dominantBoneIndex = weight.boneIndex1;
                    dominantWeight = weight.weight1;
                }

                if (weight.weight2 > dominantWeight)
                {
                    dominantBoneIndex = weight.boneIndex2;
                    dominantWeight = weight.weight2;
                }

                if (weight.weight3 > dominantWeight)
                {
                    dominantBoneIndex = weight.boneIndex3;
                    dominantWeight = weight.weight3;
                }

                if (dominantWeight <= 0f || dominantBoneIndex < 0 || dominantBoneIndex >= bones.Length)
                {
                    continue;
                }

                var bone = bones[dominantBoneIndex];
                if (bone == null || !UvDiagnosticBoneNames.Contains(bone.name))
                {
                    continue;
                }

                var uv = uvs[vertexIndex];
                if (!bounds.TryGetValue(bone.name, out var uvBounds))
                {
                    bounds[bone.name] = new[] { uv, uv };
                    vertexCounts[bone.name] = 1;
                    continue;
                }

                uvBounds[0] = Vector2.Min(uvBounds[0], uv);
                uvBounds[1] = Vector2.Max(uvBounds[1], uv);
                vertexCounts[bone.name]++;
            }

            foreach (var boneName in UvDiagnosticBoneNames)
            {
                if (bounds.TryGetValue(boneName, out var uvBounds))
                {
                    logger.LogInfo(
                        $"Lucy UV diagnostic: renderer={renderer.name}; bone={boneName}; " +
                        $"vertices={vertexCounts[boneName]}; uvBounds=min({uvBounds[0].x:0.000},{uvBounds[0].y:0.000})-" +
                        $"max({uvBounds[1].x:0.000},{uvBounds[1].y:0.000}).");
                }
            }
        }

        private void SetNeutralWhite()
        {
            foreach (var material in _coatMaterials)
            {
                if (material != null)
                {
                    SetNeutralWhite(material);
                }
            }
        }

        private static void SetNeutralWhite(Material material)
        {
            material.color = CoatColorMultiplier;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", CoatColorMultiplier);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", CoatShadowFill);
                material.EnableKeyword("_EMISSION");
            }
        }

        private void OnDestroy()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.Destroy(material);
                }
            }

            foreach (var texture in _textures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            foreach (var marking in _facialMarkings)
            {
                if (marking != null)
                {
                    UnityEngine.Object.Destroy(marking);
                }
            }

            foreach (var mesh in _markingMeshes)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            foreach (var accessory in _accessories)
            {
                if (accessory != null)
                {
                    UnityEngine.Object.Destroy(accessory);
                }
            }

            foreach (var mesh in _accessoryMeshes)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }
        }
    }
}
