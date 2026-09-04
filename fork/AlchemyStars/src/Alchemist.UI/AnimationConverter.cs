using Alchemist.InverseKinematics;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Alchemist.UI
{
    public class AnimationConverter
    {
        public static Graphics3DTranslatorFactory TranslatorFactory { get; set; } = new Graphics3DTranslatorFactory().WithDefaultTranslators();

        internal static IKTwoBoneSolver? CreateIKSolver(string name, IKSettings settings, Skeleton skeleton, AnimationPlayer player)
        {
            Logging.Logger.Info($"Attempting to create IK solver: {name}");

            if (!skeleton.TryGetBone(settings.StartBoneName, out var startBone))
            {
                Logging.Logger.Warn($"Failed to locate IK start bone: {settings.StartBoneName} in the provided skeleton");
                return null;
            }
            if (!skeleton.TryGetBone(settings.MiddleBoneName, out var middleBone))
            {
                Logging.Logger.Warn($"Failed to locate IK middle bone: {settings.MiddleBoneName} in the provided skeleton");
                return null;
            }
            if (!skeleton.TryGetBone(settings.EndBoneName, out var endBone))
            {
                Logging.Logger.Warn($"Failed to locate IK end bone: {settings.EndBoneName} in the provided skeleton");
                return null;
            }
            if (!skeleton.TryGetBone(settings.TargetBoneName, out var targetBone))
            {
                Logging.Logger.Warn($"Failed to locate IK target bone: {settings.TargetBoneName} in the provided skeleton");
                return null;
            }

            if (!middleBone.IsDescendantOf(startBone) || !endBone.IsDescendantOf(middleBone))
            {
                Logging.Logger.Warn($"Rejected IK solver {name}: start/middle/end bones do not form a valid hierarchy.");
                return null;
            }

            if (targetBone == startBone || targetBone.IsDescendantOf(startBone))
            {
                Logging.Logger.Warn($"Rejected IK solver {name}: target {targetBone.Name} is inside the solved chain and would create a dependency cycle.");
                return null;
            }

            var solver = new IKTwoBoneSolver(name, startBone, middleBone, endBone, targetBone);
            player.Solvers.Add(solver);

            Logging.Logger.Info($"Created IK solver: {name}");

            return solver;
        }

        public static string Convert(
            Skeleton skeleton,
            Animation animation,
            IKSettings lSettings,
            IKSettings rSettings,
            string prefix,
            string suffix,
            string format,
            bool matchOldCallOfDuty,
            IEnumerable<Part>? sourceParts = null)
        {
            Logging.Logger.Info($"Attempting to convert: {animation.Name}");

            format = OutputFormatCatalog.Normalize(format);

            if (string.IsNullOrEmpty(animation.Name))
                throw new ArgumentException("No input file was provided to alchemist for main animation.");
            if (string.IsNullOrEmpty(animation.OutputFolder))
                throw new ArgumentException("No input file was provided to alchemist for output folder.");

            var player = new AnimationPlayer("Player");

            Logging.Logger.Info($"Attempting to load left and right pose");

            SkeletonAnimationSampler? plSampler = null;
            SkeletonAnimationSampler? prSampler = null;

            AnimationSamplerSolver? lSolver = null;
            AnimationSamplerSolver? rSolver = null;

            var mainAnimation = TranslatorFactory.Load<SkeletonAnimation>(animation.Name);

            if (matchOldCallOfDuty)
            {
                mainAnimation.TransformType = TransformType.Relative;
                mainAnimation.Targets.ForEach(x => x.TransformType = TransformType.Relative);
            }

            var mainSampler = new SkeletonAnimationSampler("Main", mainAnimation, skeleton, player);

            // Load optional animations
            if (!string.IsNullOrWhiteSpace(animation.LeftHandPoseFile))
            {
                Logging.Logger.Info($"Loading left hand pose: {animation.LeftHandPoseFile}");
                plSampler = new SkeletonAnimationSampler("PLLayer", TranslatorFactory.Load<SkeletonAnimation>(animation.LeftHandPoseFile), skeleton, player);
                Logging.Logger.Info($"Loaded left hand pose: {animation.LeftHandPoseFile}");
            }
            else
            {
                Logging.Logger.Info($"No left hand pose provided, skipping");
            }

            if (!string.IsNullOrWhiteSpace(animation.RightHandPoseFile))
            {
                Logging.Logger.Info($"Loading right hand pose: {animation.RightHandPoseFile}");
                prSampler = new SkeletonAnimationSampler("PRLayer", TranslatorFactory.Load<SkeletonAnimation>(animation.RightHandPoseFile), skeleton, player);
                Logging.Logger.Info($"Loaded right hand pose: {animation.RightHandPoseFile}");
            }
            else
            {
                Logging.Logger.Info($"No right hand pose provided, skipping");
            }

            // Force additive
            plSampler?.SetTransformType(TransformType.Additive);
            prSampler?.SetTransformType(TransformType.Additive);

            if (animation.EnableLeftHandIK)
                lSolver = CreateIKSolver("LSolver", lSettings, skeleton, player);
            if (animation.EnableRightHandIK)
                rSolver = CreateIKSolver("RSolver", rSettings, skeleton, player);

            // Add layers
            foreach (var layer in animation.Layers)
            {
                Logging.Logger.Info($"Loading layer: {layer.Name} of type: {layer.Type}");
                var anim = TranslatorFactory.Load<SkeletonAnimation>(layer.Name);

                switch(layer.Type)
                {
                    case AnimationLayerType.Additive:
                    case AnimationLayerType.GesturePose:
                        anim.TransformType = TransformType.Additive;
                        anim.Targets.ForEach(x => x.TransformType = TransformType.Additive);
                        break;
                }

                var startFrame = layer.Offset ?? 0;
                var layerSampler = new SkeletonAnimationSampler(layer.Type.ToString(), anim, skeleton)
                {
                    StartFrame = startFrame,
                };
                layerSampler.FrameCount = Math.Max(0, layerSampler.FrameCount + startFrame);
                if (startFrame > 0)
                {
                    layerSampler.Weights.Add(new(0, 0));
                    layerSampler.Weights.Add(new(startFrame - 1, 0));
                    layerSampler.Weights.Add(new(startFrame, 1));
                }
                player.AddLayer(layerSampler);
                Logging.Logger.Info($"Loaded layer: {layer.Name}");
            }

            // Pre-cache gesture layers
            var gestureLayers = player.Layers.FindAll(x =>
                string.Equals(x.Name, "gesture", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Name, "gesturepose", StringComparison.OrdinalIgnoreCase));

            Logging.Logger.Info($"Building weights from notetracks");

            foreach (var sampler in player.Layers)
            {
                if (sampler.Animation.Actions is null)
                    continue;

                // First let's see if we need to add gesture blend in helper weight to start off at 0
                // As unlike fingers, gestures don't have a blend start notetrack
                foreach (var note in sampler.Animation.Actions)
                {
                    if (note.Name == "gesture_blend_in")
                    {
                        gestureLayers.ForEach(x => x.Weights.Add(new(0, 0)));
                    }
                }

                foreach (var note in sampler.Animation.Actions)
                {
                    switch (note.Name)
                    {
                        case "ik_out_start_left_hand":
                        case "ik_in_end_left_hand":
                            Logging.Logger.Info($"Adding Left Hand IK 1.0 from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(lSolver?.Weights, note.KeyFrames, sampler.StartFrame, 1);
                            break;
                        case "ik_in_start_left_hand":
                        case "ik_out_end_left_hand":
                            Logging.Logger.Info($"Adding Left Hand IK 0.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(lSolver?.Weights, note.KeyFrames, sampler.StartFrame, 0);
                            break;
                        case "ik_out_start_right_hand":
                        case "ik_in_end_right_hand":
                            Logging.Logger.Info($"Adding Right Hand IK 1.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(rSolver?.Weights, note.KeyFrames, sampler.StartFrame, 1);
                            break;
                        case "ik_in_start_right_hand":
                        case "ik_out_end_right_hand":
                            Logging.Logger.Info($"Adding Right Hand IK 0.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(rSolver?.Weights, note.KeyFrames, sampler.StartFrame, 0);
                            break;
                        case "fingers_out_start_left_hand":
                        case "fingers_in_end_left_hand":
                            Logging.Logger.Info($"Adding Left Hand Finger 1.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(plSampler?.Weights, note.KeyFrames, sampler.StartFrame, 1);
                            break;
                        case "fingers_in_start_left_hand":
                        case "fingers_out_end_left_hand":
                            Logging.Logger.Info($"Adding Left Hand Finger 0.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(plSampler?.Weights, note.KeyFrames, sampler.StartFrame, 0);
                            break;
                        case "fingers_out_start_right_hand":
                        case "fingers_in_end_right_hand":
                            Logging.Logger.Info($"Adding Right Hand Finger 1.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(prSampler?.Weights, note.KeyFrames, sampler.StartFrame, 1);
                            break;
                        case "fingers_in_start_right_hand":
                        case "fingers_out_end_right_hand":
                            Logging.Logger.Info($"Adding Right Hand Finger 0.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            AddShiftedWeights(prSampler?.Weights, note.KeyFrames, sampler.StartFrame, 0);
                            break;
                        case "gesture_blend_in":
                        case "gesture_blend_out_start":
                            Logging.Logger.Info($"Adding Gesture 1.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            gestureLayers.ForEach(layer => AddShiftedWeights(layer.Weights, note.KeyFrames, sampler.StartFrame, 1));
                            break;
                        case "gesture_blend_out_end":
                            Logging.Logger.Info($"Adding Gesture 0.0 weights from: {note.Name} ({note.KeyFrames.Count} frames)");
                            gestureLayers.ForEach(layer => AddShiftedWeights(layer.Weights, note.KeyFrames, sampler.StartFrame, 0));
                            break;
                    }
                }
            }

            Logging.Logger.Info($"Built weights from notetracks");

            // Ensure the weights we just built are in-order
            player.Layers.ForEach(x => x.Weights.Sort((x, y) => x.Frame.CompareTo(y.Frame)));
            player.Solvers.ForEach(x => x.Weights.Sort((x, y) => x.Frame.CompareTo(y.Frame)));

            //var v = new (Vector3, Quaternion)[skeleton.Bones.Count];

            //skeleton.InitializeAnimationTransforms();
            //player.Update(player.FrameCount, AnimationSampleType.AbsoluteFrameTime);

            //for (int k = 0; k < skeleton.Bones.Count; k++)
            //{
            //    v[k] = (skeleton!.Bones[k].LocalTranslation, skeleton!.Bones[k].LocalRotation);
            //}

            //var finalFrame = player.FrameCount;

            //for (int i = 0; i < player.FrameCount; i++)
            //{
            //    skeleton.InitializeAnimationTransforms();
            //    player.Update(i, AnimationSampleType.AbsoluteFrameTime);

            //    bool minChanges = true;

            //    for (int k = 0; k < skeleton.Bones.Count; k++)
            //    {
            //        var a = skeleton!.Bones[k].LocalTranslation;
            //        var b = skeleton!.Bones[k].LocalRotation;

            //        var a0 = v[k].Item1;
            //        var b0 = v[k].Item2;
            //        var d0 = Vector3.DistanceSquared(a0, a);
            //        var d1 = Quaternion.Dot(b0, b);

            //        if (Quaternion.Dot(b0, b) < 0.9)
            //            minChanges = false;
            //        if (Vector3.DistanceSquared(a0, a) > (1 * 1))
            //            minChanges = false;
            //    }

            //    if (minChanges)
            //    {
            //        finalFrame = i;
            //        break;
            //    }
            //}

            Logging.Logger.Info($"Generating output animation with {player.FrameCount} frames and {skeleton.Bones.Count} bones");

            var newAnim = new SkeletonAnimation(animation.OutputName, skeleton)
            {
                TransformType = TransformType.Absolute,
                Framerate = animation.OutputFramerate,
            };

            Logging.Logger.Info($"Generating frames");

            for (int i = 0; i < player.FrameCount; i++)
            {
                skeleton.InitializeAnimationTransforms();
                player.Update(i, AnimationSampleType.AbsoluteFrameTime);

                for (int k = 0; k < skeleton.Bones.Count; k++)
                {
                    newAnim.Targets[k].AddTranslationFrame(i, skeleton!.Bones[k].LocalTranslation);
                    newAnim.Targets[k].AddRotationFrame(i, skeleton!.Bones[k].LocalRotation);
                }
            }

            Logging.Logger.Info($"Generated frames");

            //// Ensure we add the last frame
            //for (int i = 0; i < skeleton.Bones.Count; i++)
            //{
            //    newAnim.Targets[i].AddTranslationFrame(finalFrame + 2, v[i].Item1);
            //    newAnim.Targets[i].AddRotationFrame(finalFrame + 2, v[i].Item2);
            //}

            Logging.Logger.Info($"Copying notetracks");

            // Copy notetracks
            foreach (var layer in player.Layers)
            {
                if (layer.Animation.Actions is null)
                    continue;

                foreach (var note in layer.Animation.Actions)
                {
                    switch (note.Name)
                    {
                        case "ik_out_start_left_hand":
                        case "ik_in_end_left_hand":
                        case "ik_in_start_left_hand":
                        case "ik_out_end_left_hand":
                        case "ik_out_start_right_hand":
                        case "ik_in_end_right_hand":
                        case "ik_in_start_right_hand":
                        case "ik_out_end_right_hand":
                        case "fingers_out_start_left_hand":
                        case "fingers_in_end_left_hand":
                        case "fingers_in_start_left_hand":
                        case "fingers_out_end_left_hand":
                        case "gesture_blend_in":
                        case "fingers_out_start_right_hand":
                        case "fingers_in_end_right_hand":
                        case "fingers_in_start_right_hand":
                        case "fingers_out_end_right_hand":
                        case "gesture_blend_out_start":
                        case "gesture_blend_out_end":
                            Logging.Logger.Info($"Skipped notetrack: {note.Name}");
                            break;
                        default:
                            Logging.Logger.Info($"Created notetrack: {note.Name}");
                            newAnim.CreateAction(
                                note.Name,
                                note.KeyFrames.Select(x => new AnimationKeyFrame<float, Action<Graphics3DScene>?>(
                                    x.Frame + layer.StartFrame,
                                    x.Value)));
                            break;
                    }
                }
            }

            var outputFull = Path.Combine(animation.OutputFolder, prefix + animation.OutputName + suffix + format);

            Logging.Logger.Info($"Copied notetracks");
            Logging.Logger.Info($"Saving animation to: {outputFull}");

            Directory.CreateDirectory(animation.OutputFolder);

            if (string.Equals(format, ".cast", StringComparison.OrdinalIgnoreCase)
                && sourceParts is not null)
            {
                MayaCastPackage.Save(
                    outputFull,
                    sourceParts,
                    newAnim,
                    TranslatorFactory);
            }
            else if (string.Equals(format, ".smd", StringComparison.OrdinalIgnoreCase))
            {
                SaveAtomically(outputFull, temporaryPath => SmdAnimationExporter.Save(temporaryPath, newAnim));
            }
            else if (string.Equals(format, ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                if (sourceParts is null)
                    throw new InvalidOperationException(LocalizationManager.Get("FbxNeedsModels"));
                SaveAtomically(outputFull, temporaryPath =>
                {
                    var stagingDirectory = Path.Combine(
                        Path.GetDirectoryName(temporaryPath)!,
                        $".alchemy-stars-fbx-{Guid.NewGuid():N}");
                    var stagedFbx = Path.Combine(stagingDirectory, Path.GetFileName(outputFull));
                    var stagedCast = Path.ChangeExtension(stagedFbx, ".cast");
                    try
                    {
                        Directory.CreateDirectory(stagingDirectory);
                        MayaCastPackage.Save(stagedCast, sourceParts, newAnim, TranslatorFactory);
                        MayaFbxExporter.Export(stagedCast, stagedFbx, newAnim.Framerate);
                        File.Move(stagedFbx, temporaryPath, overwrite: true);
                    }
                    finally
                    {
                        if (Directory.Exists(stagingDirectory))
                            Directory.Delete(stagingDirectory, recursive: true);
                    }
                });
            }
            else
            {
                SaveAtomically(outputFull, temporaryPath => TranslatorFactory.Save(temporaryPath, newAnim));
            }

            Logging.Logger.Info($"Saved animation to: {outputFull}");
            return outputFull;
        }

        /// <summary>
        /// Loads a combined skeleton from the provided parts.
        /// </summary>
        /// <param name="parts">The parts to load skeletons from.</param>
        /// <returns>A single combined skeleton.</returns>
        public static Skeleton LoadSkeletonFromParts(IEnumerable<Part> parts, bool matchOldCallOfDuty) =>
            MergedSkeletonBuilder.Build(parts, matchOldCallOfDuty, TranslatorFactory);

        private static void SaveAtomically(string outputPath, Action<string> write)
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullOutputPath)
                ?? throw new InvalidOperationException("Output path has no directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(fullOutputPath)}");
            try
            {
                write(temporaryPath);
                File.Move(temporaryPath, fullOutputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void AddShiftedWeights(
            List<AnimationKeyFrame<float, float>>? destination,
            IEnumerable<AnimationKeyFrame<float, Action<Graphics3DScene>?>> source,
            float frameOffset,
            float value)
        {
            if (destination is null)
                return;

            destination.AddRange(source.Select(frame =>
                new AnimationKeyFrame<float, float>(frame.Frame + frameOffset, value)));
        }
    }
}
