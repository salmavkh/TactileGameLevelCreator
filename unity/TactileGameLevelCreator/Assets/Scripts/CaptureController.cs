using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class CaptureController : MonoBehaviour
{
    [Header("UI")]
    public RawImage webcamView;
    public Button captureButton;
    public Button retakeButton;
    public Button continueButton;

    [Header("Camera (optional)")]
    [Tooltip("Leave empty to use first camera. Example keyword: USB, Logitech")]
    public string preferredCameraKeyword = "USB";

    [Header("Segmenter Script")]
    [Tooltip("Full path to venv python. Example: /Users/you/.../segmentation/.venv/bin/python")]
    public string pythonExePath;

    [Tooltip("Full path to your script. Example: /Users/you/.../segmentation/process_img_for_unity.py")]
    public string segmenterScriptPath;

    WebCamTexture webcamTex;
    Texture2D capturedFrame;

    void Start()
    {
        // Prevent webcam RawImage from blocking button clicks
        if (webcamView != null)
            webcamView.raycastTarget = false;

        // List cameras
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogError("No webcams found.");
            return;
        }

        foreach (var d in devices)
            Debug.Log("Camera: " + d.name);

        // Pick camera
        string chosen = devices[0].name;
        if (!string.IsNullOrWhiteSpace(preferredCameraKeyword))
        {
            foreach (var d in devices)
            {
                if (d.name.ToLower().Contains(preferredCameraKeyword.ToLower()))
                {
                    chosen = d.name;
                    break;
                }
            }
        }

        webcamTex = new WebCamTexture(chosen);
        webcamView.texture = webcamTex;
        webcamTex.Play();

        SetButtons(captured: false);
        Debug.Log("Capture scene ready. Using camera: " + chosen);
    }

    void SetButtons(bool captured)
    {
        captureButton.interactable = !captured;
        retakeButton.interactable = captured;
        continueButton.interactable = captured;
    }

    public void Capture()
    {
        Debug.Log("Capture clicked");

        if (webcamTex == null || !webcamTex.isPlaying)
        {
            Debug.LogWarning("Webcam not running.");
            return;
        }

        // Sometimes width/height are 0 for a moment after Play()
        if (webcamTex.width <= 16 || webcamTex.height <= 16)
        {
            Debug.LogWarning($"Webcam not ready yet. width={webcamTex.width}, height={webcamTex.height}");
            return;
        }

        capturedFrame = new Texture2D(webcamTex.width, webcamTex.height, TextureFormat.RGB24, false);
        capturedFrame.SetPixels(webcamTex.GetPixels());
        capturedFrame.Apply();

        webcamTex.Stop();
        webcamView.texture = capturedFrame;

        SetButtons(captured: true);
    }

    public void Retake()
    {
        Debug.Log("Retake clicked");

        capturedFrame = null;

        if (webcamTex != null)
        {
            webcamView.texture = webcamTex;
            if (!webcamTex.isPlaying)
                webcamTex.Play();
        }

        SetButtons(captured: false);
    }

    public void Continue()
    {
        Debug.Log("Continue clicked");

        if (capturedFrame == null)
        {
            Debug.LogWarning("No captured frame.");
            return;
        }

        if (string.IsNullOrWhiteSpace(pythonExePath) || string.IsNullOrWhiteSpace(segmenterScriptPath))
        {
            Debug.LogError("pythonExePath or segmenterScriptPath not set in Inspector.");
            return;
        }

        // 1) Create run folder
        string runDir = Path.Combine(
            Application.persistentDataPath,
            "run_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss")
        );
        Directory.CreateDirectory(runDir);

        // 2) Save captured.png
        string inputPath = Path.Combine(runDir, "captured.png");
        File.WriteAllBytes(inputPath, capturedFrame.EncodeToPNG());
        Debug.Log("Saved captured image: " + inputPath);

        // 3) Run segmentation python
        RunPythonSegmenter(inputPath, runDir);
    }

    void RunPythonSegmenter(string inputPath, string outDir)
    {
        string workDir = Path.GetDirectoryName(segmenterScriptPath); // segmentation folder

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = pythonExePath,
            Arguments = $"\"{segmenterScriptPath}\" --in \"{inputPath}\" --out \"{outDir}\"",
            WorkingDirectory = workDir,               // IMPORTANT
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Debug.Log("Running python:\n" + psi.FileName + " " + psi.Arguments + "\nWD=" + psi.WorkingDirectory);

        try
        {
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.Log("Python exit code: " + proc.ExitCode);

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log("[PYTHON OUT]\n" + stdout);

                if (!string.IsNullOrEmpty(stderr))
                {
                    if (proc.ExitCode == 0)
                        Debug.LogWarning("[PYTHON STDERR]\n" + stderr);   // warnings only
                    else
                        Debug.LogError("[PYTHON ERR FULL]\n" + stderr);   // real error
                }

                if (proc.ExitCode != 0)
                {
                    Debug.LogError("Python failed. See stderr above.");
                    return;
                }

            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to run Python: " + ex.Message);
            return;
        }

        // Verify outputs
        string pngPath = Path.Combine(outDir, "objects_only_rgba.png");
        string jsonPath = Path.Combine(outDir, "objects_contour.json");

        if (!File.Exists(pngPath))
        {
            Debug.LogError("Missing output PNG: " + pngPath);
            return;
        }
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("Missing output JSON: " + jsonPath);
            return;
        }

        SessionManager.RunDir = outDir;
        SceneManager.LoadScene("Customize");
    }


    void OnDisable()
    {
        if (webcamTex != null && webcamTex.isPlaying)
            webcamTex.Stop();
    }
}
