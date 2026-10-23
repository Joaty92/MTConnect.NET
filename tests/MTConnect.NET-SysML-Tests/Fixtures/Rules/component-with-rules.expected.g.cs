// Copyright (c) 2024 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

// MTConnect SysML v2.3 : UML ID = uml-cr-1

namespace MTConnect.Devices.Components
{
    /// <summary>
    /// A component with rules.
    /// </summary>
    public class RuledComponent : Component
    {
        /// <summary>
        /// The MTConnect <c>type</c> value that identifies this Component.
        /// </summary>
        public const string TypeId = "RULED";

        /// <summary>
        /// The default <c>name</c> assigned to an instance of this Component.
        /// </summary>
        public const string NameId = "ruled";

        /// <summary>
        /// The description of this Component as defined by the MTConnect Standard.
        /// </summary>
        public new const string DescriptionText = "A component with rules.";

        /// <summary>
        /// The description of this Component as defined by the MTConnect Standard.
        /// </summary>
        public override string TypeDescription => DescriptionText;

        /// <summary>
        /// The OCL constraint bodies attached to this Component in the
        /// source SysML model, preserved verbatim so downstream consumers
        /// can inspect the spec's raw validation rules at runtime.
        /// </summary>
        public new static readonly string[] Rules = new[]
        {
            "self.nested->notEmpty()"
        };


        /// <summary>
        /// Initializes a new instance with its <c>Type</c> set to <see cref="TypeId"/>.
        /// </summary>
        public RuledComponent()
        {
            Type = TypeId;
        }
    }
}
