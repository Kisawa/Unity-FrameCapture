# Unity-FrameCapture
## Screenshot tool
>supports transparent channel export
>support bloom post, extensible custom post-processing with IFrameCaptureFeatureHandle interface
>timeline track

__Default output directory:__

![50f82535-901b-415e-bed3-48ecca3403c6](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/ea4e365c-941e-4bd6-bfa1-3bcc94b6f68d)


add the Frame Capture Feature:
(Adjust to the appropriate preview Settings when using bloom or Transparent-Effect, if you understand how layer blending works)

__Common blend mode:__
* SrcBlend: SrcAlpha  &  DstBlend: OneMinusSrcAlpha
* SrcBlend: One  &  DstBlend: OneMinusSrcAlpha
* SrcBlend: SrcAlpha  &  DstBlend: One
* SrcBlend: One  &  DstBlend: One


![134f2377-2eec-46bc-8440-8068addd6dbd](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/bf77fcc9-1b60-40b3-8afa-35bc0b89c43e)

enable transparent and open preview: (Normal.scene)

![fb2a3d86-a73b-4b57-85b4-07bb9530a8aa](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/4c3d8a3a-7ca9-4bd4-8d89-06f31a5d106a)

timeline extension:

![96db3b75084946221642dbf7af0c6333](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/d4149b41-0a3c-4c61-a0f9-0cdde77ad50c)

the frame capture track settings: 
(will temporarily take over the feature's Settings while playing)

![image](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/40b7a18e-a063-4119-af44-01a4f6568108)

the clip settings:  (click Start to play timeline and screenshot)

![image](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/1e3662c9-977c-43ba-9257-08030c306fd2)

Timeline Extension example:  (Timeline.scene)

![35a6644f-87c0-496d-aeec-4d1d8368afde](https://github.com/Kisawa/Unity-FrameCapture/assets/71002504/c080a85b-b245-4dd4-bdf7-782d4eac0302)

