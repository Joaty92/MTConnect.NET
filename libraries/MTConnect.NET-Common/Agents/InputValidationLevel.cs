// Copyright (c) 2023 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

namespace MTConnect.Agents
{
    /// <summary>
    /// Controls how the Agent reacts when an observation or asset input fails per-DataItem validation
    /// against the DataItem's Type. Device-shape validation (Component, Composition, DataItem) is
    /// governed by <see cref="DeviceValidationLevel"/> so integrators can pick each axis independently
    /// — for example, <c>InputValidationLevel = Strict</c> alongside <c>DeviceValidationLevel = Warning</c>
    /// to reject bad observations while tolerating minor device-model shape drift.
    /// </summary>
    public enum InputValidationLevel
    {
        /// <summary>
        /// Accept invalid input unchanged; perform no validation action.
        /// </summary>
        Ignore,

        /// <summary>
        /// Accept invalid input but emit a validation warning.
        /// </summary>
        Warning,

        /// <summary>
        /// Drop the invalid input and continue processing the remainder.
        /// </summary>
        Remove,

        /// <summary>
        /// Reject the entire input on the first validation failure.
        /// </summary>
        Strict
    }
}