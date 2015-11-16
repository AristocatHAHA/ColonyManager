﻿// Manager/ManagerJob_Forestry.cs
// 
// Copyright Karel Kroeze, 2015.
// 
// Created 2015-11-05 22:41

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FM
{
    public class ManagerJob_Forestry : ManagerJob
    {
        private static int _histSize                     = 100;
        private readonly float _margin                   = Utilities.Margin;
        private Texture2D _cogTex                        = ContentFinder< Texture2D >.Get( "UI/Buttons/Cog" );

        public Area LoggingArea                          = null;
        public Dictionary< ThingDef, bool > AllowedTrees;
        public bool AllowSaplings                        = false;
        public bool ClearWindCells                       = true;
        public List< Designation > Designations          = new List< Designation >();
        public new Trigger_Threshold Trigger;

        public History historyShown;
        public History day                               = new History( _histSize );
        public History month                             = new History( _histSize, History.Period.Month );
        public History year                              = new History( _histSize, History.Period.Year );

        public override string Label
        {
            get { return "FMF.Forestry".Translate(); }
        }

        #region Overrides of ManagerJob

        public override void ExposeData()
        {
            // scribe base things
            base.ExposeData();
            
            // settings
            Scribe_References.LookReference(ref LoggingArea, "LoggingArea");
            Scribe_Collections.LookDictionary(ref AllowedTrees, "AllowedTrees", LookMode.DefReference, LookMode.Value);
            Scribe_Values.LookValue(ref AllowSaplings, "AllowSaplings", false);
            Scribe_Values.LookValue(ref ClearWindCells, "ClearWindCells", true);

            // trigger
            Scribe_Deep.LookDeep( ref Trigger, "Trigger", this );

            if ( Manager.LoadSaveMode == Manager.Modes.Normal )
            {
                // current designations
                Scribe_Collections.LookList(ref Designations, "Designations", LookMode.MapReference);

                // scribe history
                Scribe_Deep.LookDeep( ref day, "histDay", _histSize );
                Scribe_Deep.LookDeep( ref month, "histMonth", _histSize, History.Period.Month );
                Scribe_Deep.LookDeep( ref year, "histYear", _histSize, History.Period.Year );
            }
        }

        #endregion

        public override ManagerTab Tab
        {
            get { return Manager.Get.ManagerTabs.Find( tab => tab is ManagerTab_Forestry ); }
        }

        public override string[] Targets
        {
            get
            {
                return AllowedTrees.Keys.Where( key => AllowedTrees[key] ).Select( tree => tree.LabelCap ).ToArray();
            }
        }

        public ManagerJob_Forestry()
        {
            // populate the trigger field, set the root category to meats and allow all but human meat.
            Trigger = new Trigger_Threshold( this );
            Trigger.ThresholdFilter.SetDisallowAll();
            Trigger.ThresholdFilter.SetAllow( Utilities_Forestry.Wood, true );
            
            // populate the list of trees from the plants in the biome - allow all by default.
            // A tree is defined as any plant that yields wood
            AllowedTrees =
                Find.Map.Biome.AllWildPlants.Where( pd => pd.plant.harvestedThingDef == Utilities_Forestry.Wood )
                    .ToDictionary( pk => pk, v => true );
        }

        public override void Tick()
        {
            if ( Find.TickManager.TicksGame % day.Interval == 0 )
            {
                day.Add( Trigger.CurCount );
            }
            if ( Find.TickManager.TicksGame % month.Interval == 0 )
            {
                month.Add( Trigger.CurCount );
            }
            if ( Find.TickManager.TicksGame % year.Interval == 0 )
            {
                year.Add( Trigger.CurCount );
            }
        }

        /// <summary>
        /// Remove obsolete designations from the list.
        /// </summary>
        public void CleanDesignations()
        {
            // get the intersection of bills in the game and bills in our list.
            List< Designation > gameDesignations =
                Find.DesignationManager.DesignationsOfDef( DesignationDefOf.HarvestPlant ).ToList();
            Designations = Designations.Intersect( gameDesignations ).ToList();
        }

        public override void CleanUp()
        {
            // clear the list of obsolete designations
            CleanDesignations();

            // cancel outstanding designation
            foreach ( Designation des in Designations )
            {
                des.Delete();
            }

            // clear the list completely
            Designations.Clear();
        }

        // TODO: Refactor into utilities - hardly any changes between Forestry / Hunting / Production.
        public override void DrawListEntry( Rect rect, bool overview = true, bool active = true )
        {
            // (detailButton) | name | bar | last update

            Rect labelRect = new Rect( _margin, _margin,
                                       rect.width -
                                       ( active ? _lastUpdateRectWidth + _progressRectWidth + 4 * _margin : 2 * _margin ),
                                       rect.height - 2 * _margin ),
                 progressRect = new Rect( labelRect.xMax + _margin, _margin,
                                          _progressRectWidth,
                                          rect.height - 2 * _margin ),
                 lastUpdateRect = new Rect( progressRect.xMax + _margin, _margin,
                                            _lastUpdateRectWidth,
                                            rect.height - 2 * _margin );

            string text = Label + "\n<i>" +
                          ( Targets.Length < 4 ? string.Join( ", ", Targets ) : "<multiple>" )
                          + "</i>";

#if DEBUG
            text += Priority;
#endif

            GUI.BeginGroup( rect );
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label( labelRect, text );

                // if the bill has a manager job, give some more info.
                if ( active )
                {
                    // draw progress bar
                    Trigger.DrawProgressBar( progressRect, Suspended );

                    // draw time since last action
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label( lastUpdateRect, ( Find.TickManager.TicksGame - LastAction ).TimeString() );

                    // set tooltips
                    TooltipHandler.TipRegion( progressRect,
                                              "FMF.ThresholdCount".Translate( Trigger.CurCount, Trigger.Count ) );
                    TooltipHandler.TipRegion( lastUpdateRect,
                                              "FM.LastUpdateTooltip".Translate(
                                                  ( Find.TickManager.TicksGame - LastAction ).TimeString() ) );
                    if ( !( Targets.Length < 4 ) )
                    {
                        TooltipHandler.TipRegion( labelRect, string.Join( ", ", Targets ) );
                    }
                }
            }
            finally
            {
                // make sure everything is always properly closed / reset to defaults.
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.EndGroup();
            }
        }

        public override void DrawOverviewDetails( Rect rect )
        {
            if ( historyShown == null )
            {
                historyShown = day;
            }
            historyShown.DrawPlot( rect, Trigger.Count );

            Rect switchRect = new Rect( rect.xMax - 16f - _margin, rect.yMin + _margin, 16f, 16f );
            Widgets.DrawHighlightIfMouseover( switchRect );
            if ( Widgets.ImageButton( switchRect, _cogTex ) )
            {
                List< FloatMenuOption > options = new List< FloatMenuOption >
                {
                    new FloatMenuOption( "Day", delegate { historyShown = day; } ),
                    new FloatMenuOption( "Month", delegate { historyShown = month; } ),
                    new FloatMenuOption( "Year", delegate { historyShown = year; } )
                };
                Find.WindowStack.Add( new FloatMenu( options ) );
            }
        }

        public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.PlantCutting;

        public override bool TryDoJob()
        {
            // keep track if any actual work was done.
            bool workDone = false;

            // remove designations not in zone.
            if ( LoggingArea != null )
            {
                CleanAreaDesignations();
            }

            // clean dead designations
            CleanDesignations();

            // designate wind cells
            if ( ClearWindCells )
            {
                DesignateWindCells();
            }

            // get current lumber count
            int count = Trigger.CurCount + GetWoodInDesignations() + GetWoodLyingAround();

            // get sorted list of loggable trees
            List< Plant > trees = GetLoggableTreesSorted();

            // designate untill we're either out of trees or we have enough designated.
            for ( int i = 0; i < trees.Count && count < Trigger.Count; i++ )
            {
                workDone = true;
                AddDesignation( trees[i], DesignationDefOf.HarvestPlant );
                count += trees[i].YieldNow();
            }

            return workDone;
        }

        private void AddDesignation( Plant p, DesignationDef def = null )
        {
            // create designation
            Designation des = new Designation( p, def );

            // add to game
            Find.DesignationManager.AddDesignation( des );

            // add to internal list
            Designations.Add( des );
        }

        public void DesignateWindCells()
        {
            foreach ( IntVec3 cell in GetWindCells() )
            {
                // confirm there is a plant here that it is a tree and that it has no current designation
                Plant plant = cell.GetPlant();
                if ( plant != null &&
                     plant.def.plant.IsTree &&
                     Find.DesignationManager.DesignationOn( plant, DesignationDefOf.CutPlant ) == null )
                {
                    AddDesignation( plant, DesignationDefOf.CutPlant );
                }
            }
        }

        private List< IntVec3 > GetWindCells()
        {
            return Find.ListerBuildings
                       .AllBuildingsColonistOfClass< Building_WindTurbine >()
                       .SelectMany( turbine => Building_WindTurbine.CalculateWindCells( turbine.Position,
                                                                                        turbine.Rotation,
                                                                                        turbine.RotatedSize ) )
                       .ToList();
        }

        private void CleanAreaDesignations()
        {
            foreach ( Designation des in Designations )
            {
                if ( !des.target.HasThing )
                {
                    des.Delete();
                }
                else if ( !LoggingArea.ActiveCells.Contains( des.target.Thing.Position )
                          &&
                          ( !IsInWindTurbineArea( des.target.Thing.Position ) || !ClearWindCells ) )
                {
                    des.Delete();
                }
            }
        }

        private List< Plant > GetLoggableTreesSorted()
        {
            // we need to define a 'base' position to calculate distances.
            // Try to find a managerstation (in all non-debug cases this method will only fire if there is such a station).
            IntVec3 position = IntVec3.Zero;
            Building managerStation =
                Find.ListerBuildings.AllBuildingsColonistOfClass< Building_ManagerStation >().FirstOrDefault();
            if ( managerStation != null )
            {
                position = managerStation.Position;
            }

            // otherwise, use the average of the home area. Not ideal, but it'll do.
            else
            {
                List< IntVec3 > homeCells = Find.AreaManager.Get< Area_Home >().ActiveCells.ToList();
                for ( int i = 0; i < homeCells.Count; i++ )
                {
                    position += homeCells[i];
                }
                position.x /= homeCells.Count;
                position.y /= homeCells.Count;
                position.z /= homeCells.Count;
            }

            // get a list of alive animals that are not designated in the hunting grounds and are reachable, sorted by meat / distance * 2
            List< Plant > list = Find.ListerThings.AllThings.Where( p => p.def.plant != null
                                                                         // non-biome trees won't be on the list
                                                                         && AllowedTrees.ContainsKey( p.def )
                                                                         // also filters out non-tree plants
                                                                         && AllowedTrees[p.def]
                                                                         && p.SpawnedInWorld
                                                                         && Find.DesignationManager.DesignationOn( p ) == null
                                                                         // cut only mature trees, or saplings that yield wood.
                                                                         && ( ( AllowSaplings && ((Plant)p).YieldNow() > 1) 
                                                                            || ( (Plant)p ).LifeStage == PlantLifeStage.Mature )
                                                                         && ( LoggingArea == null 
                                                                            || LoggingArea.ActiveCells.Contains( p.Position ) )
                                                                         && p.Position.CanReachColony() )

                // OrderBy defaults to ascending, switch sign on current yield to get descending
                                     .Select( p => p as Plant )
                                     .OrderBy(
                                         p =>
                                             - p.YieldNow() /
                                             ( Math.Sqrt( position.DistanceToSquared( p.Position ) ) * 2 ) )
                                     .ToList();

            return list;
        }

        private bool IsInWindTurbineArea( IntVec3 position )
        {
            return GetWindCells().Contains( position );
        }

        public int GetWoodInDesignations()
        {
            int count = 0;

            foreach ( Designation des in Designations )
            {
                if ( des.target.HasThing &&
                     des.target.Thing is Plant )
                {
                    Plant plant = des.target.Thing as Plant;
                    count += plant.YieldNow();
                }
            }

            return count;
        }

        public int GetWoodLyingAround()
        {
            return Find.ListerThings.ThingsOfDef( Utilities_Forestry.Wood )
                       .Where( thing => !thing.IsInAnyStorage()
                                        && !thing.IsForbidden( Faction.OfColony )
                                        && !thing.Position.CanReachColony() )
                       .Sum( thing => thing.stackCount );
        }
    }
}