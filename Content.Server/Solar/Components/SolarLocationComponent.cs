using Content.Server.Solar.EntitySystems;

namespace Content.Server.Solar.Components
{

    [RegisterComponent]
    [Access(typeof(SolarPositioningSystem))]
    public sealed partial class SolarLocationComponent : Component
    {
        /// <summary>
        /// The current sun angle.
        /// </summary>
        [DataField]
        public Angle TowardsSun { get; set; } = Angle.Zero;

        /// <summary>
        /// The current sun angular velocity. (This is changed in Initialize)
        /// </summary>
        [DataField]
        public Angle SunAngularVelocity { get; set;} = Angle.Zero;
    }
}
