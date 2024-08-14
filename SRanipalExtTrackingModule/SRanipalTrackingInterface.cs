using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using ViveSR.anipal.Lip;
using VRCFaceTracking;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;
using SRanipalExtTrackingModule;
using SRanipalExtTrackingModule.TrackingModels;

namespace SRanipalExtTrackingInterface
{
    public class SRanipalExtTrackingInterface : ExtTrackingModule
    {
        LipData_v2 lipData = default;
        EyeData_v2 eyeData = default;
        private static bool eyeEnabled = false, 
                            lipEnabled = false, 
                            isViveProEye = false,
                            isWireless = false;
        private static Error eyeError = Error.UNDEFINED;
        private static Error lipError = Error.UNDEFINED;

        private static ModuleConfig moduleConfig;
        private static TrackingModel trackingModel;

        internal static Process? _process;
        internal static IntPtr _processHandle;
        internal static IntPtr _offset;
        
        private static byte[] eyeImageCache, lipImageCache;
        
        // Kernel32 SetDllDirectory
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);
        
        private static bool Attach()
        {
            var processes = Process.GetProcessesByName("sr_runtime");
            if (processes.Length <= 0) return false;
            _process = processes[0];
            _processHandle =
                Utils.OpenProcess(Utils.PROCESS_VM_READ,
                    false, _process.Id);
            return true;
        }

        private static byte[] ReadMemory(IntPtr offset, ref byte[] buf) {
            var bytesRead = 0;
            var size = buf.Length;
            
            Utils.ReadProcessMemory((int) _processHandle, offset, buf, size, ref bytesRead);

            return bytesRead != size ? null : buf;
        }

        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
            var hmdStream = a.GetManifestResourceStream("SRanipalExtTrackingModule.Assets.vive_hmd.png");
            var lipStream = a.GetManifestResourceStream("SRanipalExtTrackingModule.Assets.vive_face_tracker.png");

            // Look for SRanipal assemblies here. Placeholder for unmanaged assemblies not being embedded in the dll.
            var currentDllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            
            // Get the directory of the sr_runtime.exe program from our start menu shortcut. This is where the SRanipal dlls are located.
            var srInstallDir = (string) Registry.LocalMachine.OpenSubKey(@"Software\VIVE\SRWorks\SRanipal")?.GetValue("ModuleFileName");

            #region Remove Logs
            // Dang you SRanipal
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var srLogsDirectory = Path.Combine(localAppData + @"Low\HTC Corporation\SR_Logs\SRAnipal_Logs");

            // Get logs that should be yeeted.
            string[] srLogFiles = Directory.GetFiles(srLogsDirectory);
        
            foreach (string logFile in srLogFiles)
            {
                try {
                    using (var stream = File.Open(logFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                    {
                        Logger.LogDebug($"Clearing \"{logFile}\"");
                        stream.SetLength(0);
                        stream.Close();
                    }
                }
                catch {
                    Logger.LogWarning($"Failed to delete log file \"{logFile}\"");
                }
            }
            #endregion

            moduleConfig = ModuleJSONLoader.LoadConfig(Logger);

            if (srInstallDir == null)
            {
                Logger.LogError("Bruh, SRanipal not installed. Assuming default path");
                srInstallDir = "C:\\Program Files\\VIVE\\SRanipal\\sr_runtime.exe";
            }
            
            // Get the currently installed sr_runtime version. If it's above 1.3.6.* then we use ModuleLibs\\New
            var srRuntimeVer = "1.3.1.1";   // We'll assume 1.3.1.1 if we can't find the version.
            try
            {
                srRuntimeVer = FileVersionInfo.GetVersionInfo(srInstallDir).FileVersion;
            }
            catch
            {
                Logger.LogDebug("Smh you've got a bad install of SRanipal. Because you're like 97% likely to complain in the discord about this, I'll just assume you're using 1.3.1.1");
                Logger.LogDebug("I swear to god if you complain about this and have also fucked around with the sranipal install dir and have a version higher than 1.3.6.* I will ban you faster than my father dropped me as a child do you understand");
            }
            
            Logger.LogInformation($"SRanipalExtTrackingModule: SRanipal version: {srRuntimeVer}");
            
            SetDllDirectory(currentDllDirectory + "\\ModuleLibs\\" + (srRuntimeVer.StartsWith("1.3.6") ? "New" : "Old"));

            SRanipal_API.InitialRuntime(); // hack to unblock sranipal!!!

            eyeEnabled = moduleConfig.UseEyeTracking && InitTracker(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, "Eye");
            lipEnabled = moduleConfig.UseLipTracking && InitTracker(SRanipal_Lip_v2.ANIPAL_TYPE_LIP_V2, "Lip");

            if (eyeEnabled && Utils.HasAdmin)
            {
                var found = false;
                int tries = 0;
                while (!found && tries < 15)
                {
                    tries++;
                    found = Attach();
                    Thread.Sleep(250);
                }

                if (found)
                {
                    // Find the EyeCameraDevice.dll module inside sr_runtime, get it's offset and add hex 19190 to it for the image stream.
                    foreach (ProcessModule module in _process.Modules)
                        if (module.ModuleName == "EyeCameraDevice.dll")
                        {
                            _offset = module.BaseAddress; 
                            
                            switch (_process.MainModule?.FileVersionInfo.FileVersion)
                            {
                                case "1.3.2.0":
                                    _offset += 0x19190;
                                    UnifiedTracking.EyeImageData.SupportsImage = true;
                                    break;
                                case "1.3.1.1":
                                    _offset += 0x19100;
                                    UnifiedTracking.EyeImageData.SupportsImage = true;
                                    break;
                                default:
                                    UnifiedTracking.EyeImageData.SupportsImage = false;
                                    break;
                            }
                        }
                            
                    UnifiedTracking.EyeImageData.ImageSize = (200, 100);
                    UnifiedTracking.EyeImageData.ImageData = new byte[200 * 100 * 4];
                    eyeImageCache = new byte[200 * 100];
                }
            }
            
            if (lipEnabled)
            {
                UnifiedTracking.LipImageData.SupportsImage = true;
                UnifiedTracking.LipImageData.ImageSize = (SRanipal_Lip_v2.ImageWidth, SRanipal_Lip_v2.ImageHeight);
                lipData.image = Marshal.AllocCoTaskMem(UnifiedTracking.LipImageData.ImageSize.x *
                                                       UnifiedTracking.LipImageData.ImageSize.x);

                UnifiedTracking.LipImageData.ImageData = new byte[SRanipal_Lip_v2.ImageWidth * SRanipal_Lip_v2.ImageHeight * 4];
                lipImageCache = new byte[SRanipal_Lip_v2.ImageWidth * SRanipal_Lip_v2.ImageHeight];
            }

            ModuleInformation = new ModuleMetadata()
            {
                Name = "VIVE SRanipal",
            };
            List<Stream> streams = new List<Stream>();
            if (eyeEnabled)
                streams.Add(hmdStream);
            if (lipEnabled)
                streams.Add(lipStream);
            ModuleInformation.StaticImages = streams;

            isViveProEye = SRanipal_Eye_API.IsViveProEye();

            trackingModel = TrackingModel.GetMatchingModule(moduleConfig.TrackingModelName);

            return (eyeAvailable && eyeEnabled, expressionAvailable && lipEnabled);
        }

        private bool InitTracker(int anipalType, string name)
        {
            Logger.LogInformation($"Initializing {name}...");
            var error = SRanipal_API.Initial(anipalType, IntPtr.Zero);

            handler:
            switch (error)
            {
                case Error.FOXIP_SO: // wireless issue
                    Logger.LogInformation("Vive wireless detected. Forcing initialization...");
                    while (error == Error.FOXIP_SO)
                        error = SRanipal_API.Initial(anipalType, IntPtr.Zero);
                    goto handler;
                case Error.WORK:
                    Logger.LogInformation($"{name} successfully started!");
                    return true;
                default:
                    break;
            }
            Logger.LogInformation($"{name} failed to initialize: {error}");
            return false;
        }
        
        public override void Teardown()
        {
            SRanipal_API.ReleaseRuntime();
        }

        public override void Update()
        {
            Thread.Sleep(10);

            if (Status != ModuleState.Active)
                return;
            if (lipEnabled && !UpdateMouth())
            {
                Logger.LogError("An error has occured when updating tracking. Reinitializing needed runtimes.");
                SRanipal_API.InitialRuntime();
                InitTracker(SRanipal_Lip_v2.ANIPAL_TYPE_LIP_V2, "Lip");
            }
            if (eyeEnabled && !UpdateEye())
            {
                Logger.LogError("An error has occured when updating tracking. Reinitializing needed runtimes.");
                SRanipal_API.InitialRuntime();
                InitTracker(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, "Eye");
            }
        }

        private bool UpdateEye()
        {
            eyeError = SRanipal_Eye_API.GetEyeData_v2(ref eyeData);
            if (eyeError != Error.WORK) return false;

            trackingModel.UpdateEyeData(ref UnifiedTracking.Data.Eye, eyeData.verbose_data, isViveProEye);
            trackingModel.UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, eyeData.expression_data);

            if (_processHandle == IntPtr.Zero || !UnifiedTracking.EyeImageData.SupportsImage) 
                return true;
            
            // Read 20000 image bytes from the predefined offset. 10000 bytes per eye.
            var imageBytes = ReadMemory(_offset, ref eyeImageCache);
            
            // Concatenate the two images side by side instead of one after the other
            byte[] leftEye = new byte[10000];
            Array.Copy(imageBytes, 0, leftEye, 0, 10000);
            byte[] rightEye = new byte[10000];
            Array.Copy(imageBytes, 10000, rightEye, 0, 10000);
            
            for (var i = 0; i < 100; i++)   // 100 lines of 200 bytes
            {
                // Add 100 bytes from the left eye to the left side of the image
                int leftIndex = i * 100 * 2;
                Array.Copy(leftEye,i*100, imageBytes, leftIndex, 100);

                // Add 100 bytes from the right eye to the right side of the image
                Array.Copy(rightEye, i*100, imageBytes, leftIndex + 100, 100);
            }
            
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    byte grayscaleValue = imageBytes[y * 200 + x];

                    // Set the R, G, B, and A channels to the grayscale value
                    int index = (y * 200 + x) * 4;
                    UnifiedTracking.EyeImageData.ImageData[index + 0] = grayscaleValue; // R
                    UnifiedTracking.EyeImageData.ImageData[index + 1] = grayscaleValue; // G
                    UnifiedTracking.EyeImageData.ImageData[index + 2] = grayscaleValue; // B
                    UnifiedTracking.EyeImageData.ImageData[index + 3] = 255; // A (fully opaque)
                }
            }

            return true;
        }

        private bool UpdateMouth()
        {
            lipError = SRanipal_Lip_API.GetLipData_v2(ref lipData);
            if (lipError != Error.WORK)
                return false;
            trackingModel.UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, lipData.prediction_data);

            if (lipData.image == IntPtr.Zero || !UnifiedTracking.LipImageData.SupportsImage) 
                return true;

            Marshal.Copy(lipData.image, lipImageCache, 0, UnifiedTracking.LipImageData.ImageSize.x *
            UnifiedTracking.LipImageData.ImageSize.y);
            
            for (int y = 0; y < 400; y++)
            {
                for (int x = 0; x < 800; x++)
                {
                    byte grayscaleValue = lipImageCache[y * 800 + x];

                    // Set the R, G, B, and A channels to the grayscale value
                    int index = (y * 800 + x) * 4;
                    UnifiedTracking.LipImageData.ImageData[index + 0] = grayscaleValue; // R
                    UnifiedTracking.LipImageData.ImageData[index + 1] = grayscaleValue; // G
                    UnifiedTracking.LipImageData.ImageData[index + 2] = grayscaleValue; // B
                    UnifiedTracking.LipImageData.ImageData[index + 3] = 255; // A (fully opaque)
                }
            }

            return true;
        }
    }
}