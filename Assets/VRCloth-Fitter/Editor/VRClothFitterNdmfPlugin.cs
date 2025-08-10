using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;

[assembly: ExportsPlugin(typeof(VRClothFitter.VRClothFitterNdmfPlugin))]

namespace VRClothFitter
{
    public class VRClothFitterNdmfPlugin : Plugin<VRClothFitterNdmfPlugin>
    {
        public override string QualifiedName => "dev.omelette.vrcloth-fitter";
        public override string DisplayName => "VRCloth Fitter";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run(new ScalingPass());
        }
    }
}
