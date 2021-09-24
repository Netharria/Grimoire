// -----------------------------------------------------------------------
// <copyright file="GlobalSuppressions.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1503:Braces should not be omitted", Justification = "I believe allowing the no braces on single line if statements results in more condensed and easier to read code.")]
[assembly: SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait(false)", Justification = "This is unneccessary.")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "Regions are good for grouping code.")]
