// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

namespace MTConnect.Devices.DataItems
{
    /// <summary>
    /// Classifies a DataItem by the shape of value its Result carries, so callers can
    /// apply value-class-appropriate handling (for example, coercing an empty Result to
    /// <c>UNAVAILABLE</c> for numeric and enumeration classes while leaving arbitrary
    /// String values untouched).
    /// </summary>
    /// <remarks>
    /// The MTConnect Standard, Part 2 - Devices Information Model, ties Result validity
    /// to the DataItem's category and (for Events) its controlled vocabulary:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Samples: the Value Properties of Sample section states that
    ///     "Sample MUST always be reported in float", making every SAMPLE observation a
    ///     numeric value.</description>
    ///   </item>
    ///   <item>
    ///     <description>Events with an enumerated vocabulary (EXECUTION, CONTROLLER_MODE,
    ///     etc.): the Result MUST be a member of the controlled vocabulary defined for
    ///     that Type.</description>
    ///   </item>
    ///   <item>
    ///     <description>Events with a free-form textual value (PROGRAM, MESSAGE, TOOL_ID,
    ///     ASSET_CHANGED, and other non-vocabulary types): the standard's
    ///     Observation::result definition sets the default value type to <c>string</c>,
    ///     and does not forbid the empty string. The maintainer of the reference
    ///     C++ agent confirmed that empty-string Results are accepted for these Events
    ///     (PR #217 discussion, 2026-08-18).</description>
    ///   </item>
    /// </list>
    /// CONDITION observations are excluded from this classification; their state
    /// (Normal, Warning, Fault, Unavailable) is a separate axis handled by
    /// <see cref="MTConnect.Observations.ConditionLevel"/>.
    /// </remarks>
    public enum DataItemValueClass
    {
        /// <summary>
        /// The DataItem Result is arbitrary text. Empty and whitespace values are
        /// permitted; the SDK MUST NOT coerce them to <c>UNAVAILABLE</c>.
        /// </summary>
        String,

        /// <summary>
        /// The DataItem Result MUST be a member of the controlled vocabulary defined
        /// for its Type by the MTConnect Standard. Empty, whitespace, or off-vocabulary
        /// Results are coerced to <c>UNAVAILABLE</c> unless the configuration flag
        /// <c>AllowEmptyResultForEnumEvents</c> is set to <c>true</c>.
        /// </summary>
        Enumeration,

        /// <summary>
        /// The DataItem Result MUST parse as a number. Empty, whitespace, and
        /// non-parseable Results are coerced to <c>UNAVAILABLE</c>. All Samples fall
        /// into this class per the Part 2 "Sample MUST always be reported in float"
        /// requirement.
        /// </summary>
        Numeric
    }
}
