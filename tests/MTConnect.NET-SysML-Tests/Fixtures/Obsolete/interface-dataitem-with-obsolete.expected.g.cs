// Copyright (c) 2024 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using MTConnect.Devices;

namespace MTConnect.Interfaces
{
    /// <summary>
    /// A deprecated interface data item.
    /// </summary>
    [System.Obsolete("Deprecated in v2.5")]
    public class OldInterfaceDataItem : InterfaceDataItem
    {
        /// <summary>
        /// The MTConnect <c>category</c> (SAMPLE, EVENT, or CONDITION) of this Interface DataItem.
        /// </summary>
        public const DataItemCategory CategoryId = DataItemCategory.EVENT;

        /// <summary>
        /// The MTConnect <c>type</c> value that identifies this Interface DataItem.
        /// </summary>
        public const string TypeId = "OLD_IDI";

        /// <summary>
        /// The default <c>name</c> assigned to an instance of this Interface DataItem.
        /// </summary>
        public const string NameId = "oldIdi";

        /// <summary>
        /// The description of this Interface DataItem as defined by the MTConnect Standard.
        /// </summary>
        public new const string DescriptionText = "A deprecated interface data item.";

        /// <summary>
        /// The description of this Interface DataItem as defined by the MTConnect Standard.
        /// </summary>
        public override string TypeDescription => DescriptionText;


        /// <summary>
        /// Initializes a new instance with its category and type set to the defaults for this Interface DataItem.
        /// </summary>
        public OldInterfaceDataItem()
        {
            Category = CategoryId;
            Type = TypeId;
            
        }

        /// <summary>
        /// Initializes a new instance scoped to the given device.
        /// </summary>
        /// <param name="deviceId">The Id of the device this Interface DataItem belongs to.</param>
        public OldInterfaceDataItem(string deviceId)
        {
            Id = CreateId(deviceId, NameId);
            Category = CategoryId;
            Type = TypeId;
            Name = NameId;
        }
    }
}
