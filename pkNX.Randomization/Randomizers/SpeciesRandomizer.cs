﻿using System;
using System.Linq;
using pkNX.Structures;

namespace pkNX.Randomization
{
    public class SpeciesRandomizer
    {
        private readonly PersonalTable SpeciesStat;
        private readonly int MaxSpeciesID;
        private readonly GameInfo Game;

        private SpeciesSettings s = new();

        public SpeciesRandomizer(GameInfo game, PersonalTable t)
        {
            Game = game;
            MaxSpeciesID = Game.MaxSpeciesID;
            SpeciesStat = t;
        }

        /// <summary>
        /// Initializes the <see cref="RandSpec"/> according to the provided settings.
        /// </summary>
        /// <param name="settings">General settings</param>
        /// <param name="banlist">Optional extra: banned species</param>
        public void Initialize(SpeciesSettings settings, params int[] banlist)
        {
            s = settings;
            var list = s.GetSpecies(Game.MaxSpeciesID, Game.Generation).Except(banlist);

            legends = Game.Generation == 8 ? Legal.Legendary_8 : Legal.Legendary_1;
            events = Game.Generation == 8 ? Legal.Mythical_8 : Legal.Mythical_GG;
            RandSpec = new GenericRandomizer<int>(list.ToArray());
            RandLegend = new GenericRandomizer<int>(legends.ToArray());
            RandEvent = new GenericRandomizer<int>(events.ToArray());
        }

        #region Random Species Filtering Parameters
        private GenericRandomizer<int> RandSpec = new(Array.Empty<int>());
        private GenericRandomizer<int> RandLegend = new(Array.Empty<int>());
        private GenericRandomizer<int> RandEvent = new(Array.Empty<int>());
        private int[] legends;
        private int[] events;
        private int loopctr;
        private const int l = 10; // tweakable scalars
        private const int h = 11;
        #endregion

        internal int GetRandomSpecies(int oldSpecies, params int[] bannedSpecies)
        {
            // Get a new random species
            var oldpkm = SpeciesStat[oldSpecies];

            loopctr = 0; // altering calculations to prevent infinite loops
            int newSpecies;
            while (!GetNewSpecies(oldSpecies, oldpkm, out newSpecies) || bannedSpecies.Contains(newSpecies))
                loopctr++;
            return newSpecies;
        }

        public int GetRandomSpeciesType(int oldSpecies, int type)
        {
            // Get a new random species
            PersonalInfo oldpkm = SpeciesStat[oldSpecies];

            loopctr = 0; // altering calculations to prevent infinite loops
            int newSpecies;
            while (!GetNewSpecies(oldSpecies, oldpkm, out newSpecies) || !GetIsTypeMatch(newSpecies, type))
                loopctr++;
            return newSpecies;
        }

        private bool GetIsTypeMatch(int newSpecies, int type) => type == -1 || SpeciesStat[newSpecies].Types.Any(z => z == type) || loopctr > 9000;

        public int GetRandomSpecies() => RandSpec.Next();

        public int GetRandomSpecies(int oldSpecies)
        {
            // Get a new random species
            var oldpkm = SpeciesStat[oldSpecies];

            loopctr = 0; // altering calculations to prevent infinite loops
            int newSpecies;
            while (!GetNewSpecies(oldSpecies, oldpkm, out newSpecies))
            {
                if (loopctr > 0x0001_0000)
                {
                    var pkm = SpeciesStat[newSpecies];
                    if (IsSpeciesBSTBad(oldpkm, pkm) && loopctr > 0x0001_1000) // keep trying for at minimum BST
                        continue;
                    return newSpecies; // failed to find any match based on criteria, return random species that may or may not match criteria
                }
                loopctr++;
            }
            return newSpecies;
        }

        public int[] RandomSpeciesList => Enumerable.Range(1, MaxSpeciesID).ToArray();

        private bool GetNewSpecies(int currentSpecies, PersonalInfo oldpkm, out int newSpecies)
        {
            bool isLegend = false;

            newSpecies = RandSpec.Next();

            // If we randomly got a legendary or mythical, not really a need to reroll
            if (legends.Contains(newSpecies) || events.Contains(newSpecies)) isLegend = true;

            if ((Util.Random.Next(0, 100 + 1) < s.LegendsChance) && s.Legends)
            {
                if (!isLegend) newSpecies = RandLegend.Next();
                isLegend = true;
            }

            if ((Util.Random.Next(0, 100 + 1) < s.EventsChance) && s.Events)
            {
                if (!isLegend) newSpecies = RandEvent.Next();
            }

            var pkm = SpeciesStat[newSpecies];

            if (IsSpeciesReplacementBad(newSpecies, currentSpecies)) // no A->A randomization
                return false;
            return IsCriteriaMatch(oldpkm, pkm);
        }

        private bool IsSpeciesReplacementBad(int newSpecies, int currentSpecies)
        {
            if (newSpecies != currentSpecies)
                return false;
            return loopctr < MaxSpeciesID * 10;
        }

        private bool IsCriteriaMatch(PersonalInfo oldpkm, PersonalInfo pkm)
        {
            if (IsSpeciesEXPRateBad(oldpkm, pkm))
                return false;
            if (IsSpeciesTypeBad(oldpkm, pkm))
                return false;
            if (IsSpeciesBSTBad(oldpkm, pkm))
                return false;
            return true;
        }

        private bool IsSpeciesEXPRateBad(PersonalInfo oldpkm, PersonalInfo pkm)
        {
            return s.EXPGroup && oldpkm.EXPGrowth == pkm.EXPGrowth;
        }

        private bool IsSpeciesTypeBad(PersonalInfo oldpkm, PersonalInfo pkm)
        {
            return s.Type && oldpkm.Types.Intersect(pkm.Types).Any();
        }

        private bool IsSpeciesBSTBad(PersonalInfo oldpkm, PersonalInfo pkm)
        {
            if (!s.BST)
                return false;
            // Base stat total has to be close to original BST
            int expand = loopctr / MaxSpeciesID;
            int lo = oldpkm.BST * l / (h + expand);
            int hi = oldpkm.BST * (h + expand) / l;
            return lo > pkm.BST || pkm.BST > hi;
        }
    }
}
