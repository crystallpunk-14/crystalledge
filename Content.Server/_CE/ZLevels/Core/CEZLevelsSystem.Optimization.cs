/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Shared._CE.ZLevels.Core.Components;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    private void QuickApiCache(Entity<CEZLevelsNetworkComponent> network, EntityUid value, int depth)
    {
        var comp = network.Comp;
        var list = comp.SortedZLevels;

        // Zero handling
        if (comp.SortedMin == depth && comp.SortedMax == depth)
        {
            list.Add(value);
            return;
        }

        var min = comp.SortedMin;
        var max = comp.SortedMax;

        if (depth < min)
        {
            var delta = min - depth;
            if (delta == 1)
            {
                list.Insert(0, value);

                comp.SortedMin = depth;
                Dirty(network);
                return;
            }

            list.InsertRange(0, Enumerable.Repeat(EntityUid.Invalid, delta - 1));
            list.Insert(0, value);

            comp.SortedMin = depth;
            Dirty(network);
            return;
        }

        if (depth > max)
        {
            var delta = depth - max;
            if (delta == 1)
            {
                list.Add(value);

                comp.SortedMax = depth;
                Dirty(network);
                return;
            }

            list.AddRange(Enumerable.Repeat(EntityUid.Invalid, delta - 1));
            list.Add(value);

            comp.SortedMax = depth;
            Dirty(network);
            return;
        }

        list[depth - min] = value;
    }
}
