using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class SaveTextureToFileUtility
{
    public enum SaveTextureFileFormat
    {
        EXR, JPG, PNG, TGA
    };

    /// <summary>
    /// Saves a Texture2D to disk with the specified filename and image format
    /// </summary>
    /// <param name="tex"></param>
    /// <param name="filePath"></param>
    /// <param name="fileFormat"></param>
    /// <param name="jpgQuality"></param>
    static public void SaveTexture2DToFile(Texture2D tex, string filePath, SaveTextureFileFormat fileFormat, int jpgQuality = 95)
    {
        switch (fileFormat)
        {
            case SaveTextureFileFormat.EXR:
                System.IO.File.WriteAllBytes(filePath + ".exr", tex.EncodeToEXR());
                break;
            case SaveTextureFileFormat.JPG:
                System.IO.File.WriteAllBytes(filePath + ".jpg", tex.EncodeToJPG(jpgQuality));
                break;
            case SaveTextureFileFormat.PNG:
                System.IO.File.WriteAllBytes(filePath + ".png", tex.EncodeToPNG());
                break;
            case SaveTextureFileFormat.TGA:
                System.IO.File.WriteAllBytes(filePath + ".tga", tex.EncodeToTGA());
                break;
        }
    }


    /// <summary>
    /// Saves a RenderTexture to disk with the specified filename and image format
    /// </summary>
    /// <param name="renderTexture"></param>
    /// <param name="filePath"></param>
    /// <param name="fileFormat"></param>
    /// <param name="jpgQuality"></param>
    static public void SaveRenderTextureToFile(RenderTexture renderTexture, string filePath, SaveTextureFileFormat fileFormat = SaveTextureFileFormat.PNG, int jpgQuality = 95)
    {
        Texture2D tex;
        if (fileFormat != SaveTextureFileFormat.EXR)
            tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false, false);
        else
            tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false, true);
        var oldRt = RenderTexture.active;
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = oldRt;
        SaveTexture2DToFile(tex, filePath, fileFormat, jpgQuality);
        if (Application.isPlaying)
            Object.Destroy(tex);
        else
            Object.DestroyImmediate(tex);

    }

    static public void SaveTextureToFile(Texture source,
                                         string filePath,
                                         int width,
                                         int height,
                                         SaveTextureFileFormat fileFormat = SaveTextureFileFormat.PNG,
                                         int jpgQuality = 95,
                                         bool asynchronous = true,
                                         System.Action<bool> done = null)
    {
        // check that the input we're getting is something we can handle:
        if (!(source is Texture2D || source is RenderTexture))
        {
            done?.Invoke(false);
            return;
        }

        // use the original texture size in case the input is negative:
        if (width < 0 || height < 0)
        {
            width = source.width;
            height = source.height;
        }

        // resize the original image:
        var resizeRT = RenderTexture.GetTemporary(width, height, 0);
        Graphics.Blit(source, resizeRT);

        // create a native array to receive data from the GPU:
        var narray = new NativeArray<byte>(width * height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        // request the texture data back from the GPU:
        var request = AsyncGPUReadback.RequestIntoNativeArray(ref narray, resizeRT, 0, (AsyncGPUReadbackRequest req) =>
        {
            // if the readback was successful, encode and write the results to disk
            if (!req.hasError)
            {
                NativeArray<byte> encoded;

                switch (fileFormat)
                {
                    case SaveTextureFileFormat.EXR:
                        encoded = ImageConversion.EncodeNativeArrayToEXR(narray, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                    case SaveTextureFileFormat.JPG:
                        encoded = ImageConversion.EncodeNativeArrayToJPG(narray, resizeRT.graphicsFormat, (uint)width, (uint)height, 0, jpgQuality);
                        break;
                    case SaveTextureFileFormat.TGA:
                        encoded = ImageConversion.EncodeNativeArrayToTGA(narray, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                    default:
                        encoded = ImageConversion.EncodeNativeArrayToPNG(narray, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                }

                System.IO.File.WriteAllBytes(filePath, encoded.ToArray());
                encoded.Dispose();
            }

            narray.Dispose();

            // notify the user that the operation is done, and its outcome.
            done?.Invoke(!req.hasError);
        });

        if (!asynchronous)
            request.WaitForCompletion();
    }

}