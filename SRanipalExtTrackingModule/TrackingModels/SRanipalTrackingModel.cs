using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ViveSR.anipal.Eye;
using ViveSR.anipal.Lip;
using VRCFaceTracking.Core.Helpers;
using VRCFaceTracking.Core.Params.Data;

namespace SRanipalExtTrackingModule.TrackingModels
{
    public abstract class TrackingModel
    {
        public abstract string Name { get; }
        public abstract void UpdateEyeData(ref UnifiedEyeData eye, VerboseData external, bool isViveProEye);
        public abstract void UpdateEyeExpressions(ref UnifiedExpressionShape[] shapes, EyeExpression external);
        public abstract void UpdateMouthExpressions(ref UnifiedExpressionShape[] shapes, PredictionData_v2 external);
        public static TrackingModel GetMatchingModule(string name)
        {
            var types = Assembly.GetExecutingAssembly()
                                .GetTypes()
                                .Where(type => type.IsSubclassOf(typeof(TrackingModel)));
            Type modelType = typeof(TrackingModel);
            int distance = int.MaxValue;
            foreach (var t in types) 
            {
                if (LevenshteinDistance.Calculate(name, t.Name) < distance)
                    modelType = t;
            }

            return (TrackingModel)Activator.CreateInstance(modelType);
        }
    }
}
