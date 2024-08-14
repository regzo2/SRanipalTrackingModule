using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ViveSR.anipal.Eye;
using ViveSR.anipal.Lip;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;

namespace SRanipalExtTrackingModule.TrackingModels
{
    public class TrackingModelV1 : TrackingModel
    {
        public override string Name => "V1";

        public override void UpdateEyeData(ref UnifiedEyeData data, VerboseData external, bool isViveProEye)
        {
            if (external.left.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY))
                data.Left.Openness = external.left.eye_openness;
            if (external.right.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY))
                data.Right.Openness = external.right.eye_openness;

            if (external.left.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
                data.Left.PupilDiameter_MM = external.left.pupil_diameter_mm;
            if (external.right.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
                data.Right.PupilDiameter_MM = external.right.pupil_diameter_mm;
            
            if (isViveProEye)
            {
                if (external.left.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY))
                    data.Left.Gaze = external.left.gaze_direction_normalized.FlipXCoordinates();
                if (external.right.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY))
                    data.Right.Gaze = external.right.gaze_direction_normalized.FlipXCoordinates();
                return;
            }
            
            // Fix for Focus 3 / Droolon F1 gaze tracking. For some reason convergence data isn't available from combined set so we will calculate it from the two gaze vectors.
            if (external.left.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY) && external.right.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY))
            {
                Vector3 gaze_direction_normalized = (external.left.gaze_direction_normalized.FlipXCoordinates()/2f) + (external.right.gaze_direction_normalized.FlipXCoordinates()/2f);
                //Vector3 convergenceOffset = GetConvergenceAngleOffset(external);
                data.Left.Gaze = gaze_direction_normalized;
                data.Right.Gaze = gaze_direction_normalized;
            }
        }

        public override void UpdateEyeExpressions(ref UnifiedExpressionShape[] data, EyeExpression external)
        {
            data[(int)UnifiedExpressions.EyeWideLeft].Weight = external.left.eye_wide;
            data[(int)UnifiedExpressions.EyeWideRight].Weight = external.right.eye_wide;

            data[(int)UnifiedExpressions.EyeSquintLeft].Weight = external.left.eye_squeeze;
            data[(int)UnifiedExpressions.EyeSquintRight].Weight = external.right.eye_squeeze;

            // Emulator expressions for Unified Expressions. These are essentially already baked into Legacy eye expressions (SRanipal)
            data[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = external.left.eye_wide;
            data[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = external.left.eye_wide;

            data[(int)UnifiedExpressions.BrowInnerUpRight].Weight = external.right.eye_wide;
            data[(int)UnifiedExpressions.BrowOuterUpRight].Weight = external.right.eye_wide;

            data[(int)UnifiedExpressions.BrowPinchLeft].Weight = external.left.eye_squeeze;
            data[(int)UnifiedExpressions.BrowLowererLeft].Weight = external.left.eye_squeeze;

            data[(int)UnifiedExpressions.BrowPinchRight].Weight = external.right.eye_squeeze;
            data[(int)UnifiedExpressions.BrowLowererRight].Weight = external.right.eye_squeeze;
        }

        public override void UpdateMouthExpressions(ref UnifiedExpressionShape[] shapes, PredictionData_v2 external)
        {
            unsafe
            {
                #region Direct Jaw

                shapes[(int)UnifiedExpressions.JawOpen].Weight = external.blend_shape_weight[(int)LipShape_v2.JawOpen] + external.blend_shape_weight[(int)LipShape_v2.MouthApeShape];
                shapes[(int)UnifiedExpressions.JawLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.JawLeft];
                shapes[(int)UnifiedExpressions.JawRight].Weight = external.blend_shape_weight[(int)LipShape_v2.JawRight];
                shapes[(int)UnifiedExpressions.JawForward].Weight = external.blend_shape_weight[(int)LipShape_v2.JawForward];
                shapes[(int)UnifiedExpressions.MouthClosed].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthApeShape];

                #endregion

                #region Direct Mouth and Lip

                // These shapes have overturns subtracting from them, as we are expecting the new standard to have Upper Up / Lower Down baked into the funneller shapes below these.
                shapes[(int)UnifiedExpressions.MouthUpperUpRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperUpRight] - external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperUpRight] - external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperUpLeft] - external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperUpLeft] - external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];

                shapes[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerDownLeft] - external.blend_shape_weight[(int)LipShape_v2.MouthLowerOverturn];
                shapes[(int)UnifiedExpressions.MouthLowerDownRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerDownRight] - external.blend_shape_weight[(int)LipShape_v2.MouthLowerOverturn];

                shapes[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthPout];
                shapes[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthPout];
                shapes[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthPout];
                shapes[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthPout];

                shapes[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];
                shapes[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperOverturn];

                shapes[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperInside];
                shapes[(int)UnifiedExpressions.LipSuckUpperRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperInside];
                shapes[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerInside];
                shapes[(int)UnifiedExpressions.LipSuckLowerRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerInside];

                shapes[(int)UnifiedExpressions.MouthUpperLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperLeft];
                shapes[(int)UnifiedExpressions.MouthUpperRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthUpperRight];
                shapes[(int)UnifiedExpressions.MouthLowerLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerLeft];
                shapes[(int)UnifiedExpressions.MouthLowerRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerRight];

                shapes[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileLeft];
                shapes[(int)UnifiedExpressions.MouthCornerPullRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileRight];
                shapes[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileLeft];
                shapes[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileRight];
                shapes[(int)UnifiedExpressions.MouthFrownLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSadLeft];
                shapes[(int)UnifiedExpressions.MouthFrownRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSadRight];

                shapes[(int)UnifiedExpressions.MouthRaiserUpper].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerOverlay] - external.blend_shape_weight[(int)LipShape_v2.MouthUpperInside];
                shapes[(int)UnifiedExpressions.MouthRaiserLower].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthLowerOverlay];

                #endregion

                #region Direct Cheek

                shapes[(int)UnifiedExpressions.CheekPuffLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.CheekPuffLeft];
                shapes[(int)UnifiedExpressions.CheekPuffRight].Weight = external.blend_shape_weight[(int)LipShape_v2.CheekPuffRight];

                shapes[(int)UnifiedExpressions.CheekSuckLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.CheekSuck];
                shapes[(int)UnifiedExpressions.CheekSuckRight].Weight = external.blend_shape_weight[(int)LipShape_v2.CheekSuck];

                #endregion

                #region Direct Tongue

                shapes[(int)UnifiedExpressions.TongueOut].Weight = (external.blend_shape_weight[(int)LipShape_v2.TongueLongStep1] + external.blend_shape_weight[(int)LipShape_v2.TongueLongStep2]) / 2.0f;
                shapes[(int)UnifiedExpressions.TongueUp].Weight = external.blend_shape_weight[(int)LipShape_v2.TongueUp];
                shapes[(int)UnifiedExpressions.TongueDown].Weight = external.blend_shape_weight[(int)LipShape_v2.TongueDown];
                shapes[(int)UnifiedExpressions.TongueLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.TongueLeft];
                shapes[(int)UnifiedExpressions.TongueRight].Weight = external.blend_shape_weight[(int)LipShape_v2.TongueRight];
                shapes[(int)UnifiedExpressions.TongueRoll].Weight = external.blend_shape_weight[(int)LipShape_v2.TongueRoll];

                #endregion

                // These shapes are not tracked at all by SRanipal, but instead are being treated as enhancements to driving the shapes above.

                #region Emulated Unified Mapping

                shapes[(int)UnifiedExpressions.CheekSquintLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileLeft];
                shapes[(int)UnifiedExpressions.CheekSquintRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileRight];

                shapes[(int)UnifiedExpressions.MouthDimpleLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileLeft];
                shapes[(int)UnifiedExpressions.MouthDimpleRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSmileRight];

                shapes[(int)UnifiedExpressions.MouthStretchLeft].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSadRight];
                shapes[(int)UnifiedExpressions.MouthStretchRight].Weight = external.blend_shape_weight[(int)LipShape_v2.MouthSadRight];

                #endregion
            }
        }
    }
}
