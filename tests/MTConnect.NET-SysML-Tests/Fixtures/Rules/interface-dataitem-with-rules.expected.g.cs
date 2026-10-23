// Copyright (c) 2024 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using MTConnect.Devices;

namespace MTConnect.Interfaces
{
    /// <summary>
    /// An interface data item with rules.
    /// </summary>
    public class RuledInterfaceDataItem : InterfaceDataItem
    {
        /// <summary>
        /// The MTConnect <c>category</c> (SAMPLE, EVENT, or CONDITION) of this Interface DataItem.
        /// </summary>
        public const DataItemCategory CategoryId = DataItemCategory.EVENT;

        /// <summary>
        /// The MTConnect <c>type</c> value that identifies this Interface DataItem.
        /// </summary>
        public const string TypeId = "RULED_IDI";

        /// <summary>
        /// The default <c>name</c> assigned to an instance of this Interface DataItem.
        /// </summary>
        public const string NameId = "ruledIdi";

        /// <summary>
        /// The description of this Interface DataItem as defined by the MTConnect Standard.
        /// </summary>
        public new const string DescriptionText = "An interface data item with rules.";

        /// <summary>
        /// The description of this Interface DataItem as defined by the MTConnect Standard.
        /// </summary>
        public override string TypeDescription => DescriptionText;

        /// <summary>
        /// The OCL constraint bodies attached to this Interface DataItem
        /// in the source SysML model, preserved verbatim so downstream
        /// consumers can inspect the spec's raw validation rules at runtime.
        /// </summary>
        public new static readonly string[] Rules = new[]
        {
            "self.value >= 0"
        };


        /// <summary>
        /// Initializes a new instance with its category and type set to the defaults for this Interface DataItem.
        /// </summary>
        public RuledInterfaceDataItem()
        {
            Category = CategoryId;
            Type = TypeId;
            
        }

        /// <summary>
        /// Initializes a new instance scoped to the given device.
        /// </summary>
        /// <param name="deviceId">The Id of the device this Interface DataItem belongs to.</param>
        public RuledInterfaceDataItem(string deviceId)
        {
            Id = CreateId(deviceId, NameId);
            Category = CategoryId;
            Type = TypeId;
            Name = NameId;
        }
    }
}
