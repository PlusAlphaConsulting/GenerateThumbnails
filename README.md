---
services: functions
platforms: dotnet
author: xpouyat
---

# An Azure function that generates thumbnails from a video

This Visual Studio 2022 / VS Code Solution exposes an Azure Function that creates thumbnail(s) using ffmpeg.

Ffmpeg will read directly the video file from Azure blob storage, generate the thumbnail(s) and upload them to a storage container. This function could be used to generate thumbnails with Azure Media Services or MediaKind MK/IO assets, for example. It has been tested my MP4 and ISMV files as sources.

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

### 4. Recommended : Enable Managed Identity on the Azure Function and give it access to the storage account

To avoid using SAS urls for the input file and the output container, you can enable Managed Identity on the Azure Function and give it access to the storage account.

To do this:

- go to the Azure portal, select the Azure Function
- then go to "Identity" and enable the system-assigned identity
- Select "Azure role assignments" in the same page, and "Add role assignment":
  - Scope : Storage
  - Resource : the storage account(s) used by the function to read the video files and write the thumbnails
  - Role : "Storage Blob Data Contributor".

## Usage

Call the GenerateThumbnails function using POST and the function key.

Example JSON input body of the function :

```json
{
    "inputUrl":"https://storageaccount.blob.core.windows.net/asset-abe0685b/video.mp4",
    "outputUrl":"https://storageaccount.blob.core.windows.net/asset-abe0685b"
}
```

If you configured the system managed identity in step 4, you can use "simple" Urls. The Azure function will create SAS Urls internally to read and write the blobs.

If you did not configure managesd identity, you need to use a SAS url for the input and output URLs. The output SAS token should have read/write permissions. A SAS token can be generated from the Azure portal or the Azure Storage Explorer.

By default, the function generates one 960x540 thumbnail. You can change the number of thumbnails or their size by modifying the ffmpeg arguments in the input body:

```json
{
    "inputUrl":"https://urlofthesourceblob",
    "outputUrl":"https://urlofthedestinationcontainer",
    "ffmpegArguments" : " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg"
}
```

This parameter below will generate 5 thumbnails from the first 100 frames :

```
" -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 5 {tempFolder}\\Thumbnail%06d.jpg"
```

![5thumbnails](https://github.com/xpouyat/GenerateThumbnails/assets/8104205/7a043cb2-8911-4418-86f2-02d34de46452)

This parameter below will generate a single sprite file (tile) with a thumbnail every 30 frames :

```
" -i {input} -vf select='not(mod(n\\,30))',scale=160:90,tile -frames:v 1 preview.jpg"
```

![preview](https://github.com/xpouyat/GenerateThumbnails/assets/8104205/9799c7d6-ff3b-4d70-b94a-364aa5a82265)

Please escape correctly the backslashes and other special characters in the JSON string.

For more information about thumbnail generation, see [this page](https://trac.ffmpeg.org/wiki/Create%20a%20thumbnail%20image%20every%20X%20seconds%20of%20the%20video) and [this page for sprite encoding](https://www.ffmpegbyexample.com/examples/90permbg/generate_tile_thumbnail_picture_every_30_frames_of_video/).
