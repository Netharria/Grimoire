// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grimoire.Discord.Extensions;
internal static class StringExtension
{
    internal static string UnicodeToUTF8(this string str)
    {
        var bytSrc = Encoding.Unicode.GetBytes(str);
        var bytDestination = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, bytSrc);
        return Encoding.UTF8.GetString(bytDestination);
    }

    internal static string UTF8toUnicode(this string str)
    {
        var bytSrc = Encoding.Unicode.GetBytes(str);
        var bytDestination = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, bytSrc);
        return Encoding.Unicode.GetString(bytDestination);
    }
}
