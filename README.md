---
services: media-services,functions
platforms: dotnetcore
author: xpouyat
---

# Generates thumbnails for videos using Azure Functions

This Visual Studio 2022 Solution exposes an Azure Function that create thumbnail(s) with ffmpeg. [Azure Functions Premium plan](https://docs.microsoft.com/en-us/azure/azure-functions/functions-premium-plan
) may be needed if video file is large.

## Fork and download a copy

If not already done : fork the repo, download a local copy.

## Ffmpeg

Download ffmpeg from the Internet and copy ffmpeg.exe in \GenerateThumbnails\ffmpeg folder.
In Visual Studio, open the file properties, "Build action" should be "Content", with "Copy to Output Direcotry" set to "Copy if newer".

## Publish the function to Azure

Open the solution with Visual Studio and publish the functions to Azure.
It is recommended to use a **premium plan** to avoid functions timeout (Premium gives you 30 min and a more powerfull host).
It is possible to [unbound run duration](https://docs.microsoft.com/en-us/azure/azure-functions/functions-premium-plan#longer-run-duration).

JSON input body of the function :

```json
{
    "inputUrl":"https://mysasurlofthesourceblob",
    "outputUrl":"https://mysasurlofthedestinationcontainer",
    "ffmpegArguments" : " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg"
}
```
