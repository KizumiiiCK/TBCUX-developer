using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class AuraController
{
    public static PostProcessVolume FindAnAura()=>GameObject.Find("MainPostProcess")?.GetComponent<PostProcessVolume>();
    public static void SetUpAura(PostProcessVolume ppv, Aura A)
    {
        if (ppv == null || A == null) return;
        PostProcessProfile profile = ppv.profile;

        switch (A.AuraType)
        {
            case PostProcessType.none: break;
            case PostProcessType.bloom:
                Bloom b = profile.GetSetting<Bloom>();
                b.active = true;
                b.intensity.value = A.Parameters.x;
                b.threshold.value = A.Parameters.y;
                b.diffusion.value = A.Parameters.z;
                b.anamorphicRatio.value = A.Parameters.w;
                b.color.value = A.AuraColor;
                break;
            case PostProcessType.vignette:
                Vignette v= profile.GetSetting<Vignette>();
                v.active = true;
                v.color.value = A.AuraColor;
                v.intensity.value = A.Parameters.x;
                v.smoothness.value = A.Parameters.y;
                v.roundness.value = A.Parameters.z;
                break;
            case PostProcessType.grading:
                ColorGrading cg = profile.GetSetting<ColorGrading>();
                cg.active = true;
                cg.colorFilter.value = A.AuraColor;
                cg.hueShift.value = A.Parameters.x;
                cg.saturation.value = A.Parameters.y;
                cg.brightness.value = A.Parameters.z;
                cg.contrast.value = A.Parameters.w;
                break;
            case PostProcessType.grain:
                Grain g= profile.GetSetting<Grain>();
                g.active = true;
                g.intensity.value = A.Parameters.x;
                g.size.value = A.Parameters.y;
                g.lumContrib.value = A.Parameters.z;
                break;
            case PostProcessType.chromatic:

                break;
            case PostProcessType.motionblur:
                MotionBlur mb = profile.GetSetting<MotionBlur>();
                mb.active = true;
                mb.shutterAngle.value = A.Parameters.x;
                mb.sampleCount.value = (int)A.Parameters.y;
                break;
            default: break;
        }
    }
}
