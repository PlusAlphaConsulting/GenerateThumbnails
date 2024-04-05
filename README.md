---
services: functions
platforms: dotnet
author: xpouyat
---

# An Azure function that generates thumbnails from a video

This Visual Studio 2022 / VS Code Solution exposes an Azure Function that creates thumbnail(s) using ffmpeg.

Ffmpeg will read directly the video file from Azure blob storage, generate the thumbnail(s) and upload them to a storage container. This function could be used to generate thumbnails with MediaKind MK/IO, for example.

## How to publish the function to Azure

### 1. Fork and download a copy

If not already done : fork the repo, download a local copy.

### 2. Copy ffmpeg to ffmpeg folder

Download ffmpeg from the Internet and copy ffmpeg.exe to \GenerateThumbnails\ffmpeg folder.
In Visual Studio, open the file properties, "Build action" should be "Content", with "Copy to Output Directory" set to "Copy if newer".

### 3. Publish the function to Azure

Open the solution with Visual Studio or VS Code and publish the functions to Azure (.NET 8.0 isolated, Windows OS).
It may be needed to use a **premium plan** to avoid functions timeout (Premium gives you 30 min and a more powerfull host).
It is possible to [unbound run duration](https://docs.microsoft.com/en-us/azure/azure-functions/functions-premium-plan#longer-run-duration).

## Usage

Call the GenerateThumbnails function using POST.

Example JSON input body of the function :

```json
{
    "inputUrl":"https://mysasurlofthesourceblob",
    "outputUrl":"https://mysasurlofthedestinationcontainer"
}
```

A SAS url is needed for the input and output URLs. The output SAS token should have read/write permissions. A SAS token can be generated from the Azure portal or using the Azure Storage Explorer. I am planning to add the ability to generate SAS tokens in the function itself.

By default, the function generates one 960x540 thumbnail. You can change the number of thumbnails or size by adding and modifying the ffmpeg arguments in the input body:

```json
{
    "inputUrl":"https://mysasurlofthesourceblob",
    "outputUrl":"https://mysasurlofthedestinationcontainer",
    "ffmpegArguments" : " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg"
}
```

This parameter will generate 5 thumbnails from the first 100 frames :

```
" -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 5 {tempFolder}\\Thumbnail%06d.jpg"
```

For more information about thumbnail generation, see [this page](https://trac.ffmpeg.org/wiki/Create%20a%20thumbnail%20image%20every%20X%20seconds%20of%20the%20video).
