using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Client
{
    // The void abyss swallows light (2026-11-25, Docs/BiomeRework.md 4-1 공허의 심연: "빛이 줄어든다, 횃불·등불 반지름
    // −30%, 화면만", 묶음 B4b): a torch, a lamp, a fire or a shot's light standing in the abyss reaches TorchDim as far,
    // and the light a player carries there CarriedDim as far (less, so the screen's height round them still reads on a
    // phone held upright). The screen's alone: nothing of the rules changes. Shared by the view and the preview tool.
    static class LavaVoidLight
    {
        public const float TorchDim = 0.7f;
        public const float CarriedDim = 0.85f;

        // The lights gathered for a window, those in the abyss dimmed in place. carried: how many at the front of the list
        // are carried lights (the players').
        public static int Dim(List<LightSource> lights, Func<int, int, Biome> biomeAt, int carried)
        {
            if (biomeAt == null) return 0;
            int dimmed = 0;
            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (biomeAt(TileMap.Cell(light.Position.X), TileMap.Cell(light.Position.Y)) != Biome.Abyss) continue;
                float scale = i < carried ? CarriedDim : TorchDim;
                lights[i] = new LightSource(light.Position, light.Radius * scale, light.Intensity, light.Color, light.Tints);
                dimmed++;
            }
            return dimmed;
        }
    }
}
