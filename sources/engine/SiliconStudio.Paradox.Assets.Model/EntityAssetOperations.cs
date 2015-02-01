﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiliconStudio.Assets;
using SiliconStudio.Assets.Diff;
using SiliconStudio.Core.IO;
using SiliconStudio.Paradox.Assets.Model.Analysis;
using SiliconStudio.Paradox.Engine;
using SiliconStudio.Paradox.EntityModel;

namespace SiliconStudio.Paradox.Assets.Model
{
    public static class EntityAssetOperations
    {
        public static EntityAsset ExtractSceneClone(EntityAsset source, Guid sourceRootEntity)
        {
            if (source == null) throw new ArgumentNullException("source");

            if (source.Hierarchy.RootEntity != sourceRootEntity)
                throw new NotImplementedException("Currently, only cloning a root entity is supported.");

            return (EntityAsset)AssetCloner.Clone(source);
        }

        static IEnumerable<Entity> EnumerateChildren(this Entity entity)
        {
            var transformationComponent = entity.Get(TransformationComponent.Key);
            if (transformationComponent == null)
                yield break;


            foreach (var child in transformationComponent.Children)
            {
                yield return child.Entity;
            }
        }

        static Entity FindParent(EntityHierarchyData hierarchy, Entity entity)
        {
            // Note: we could also use a cache, but it's probably not worth it... (except if we had tens of thousands of new objects at once)
            // Let's optimize if really needed
            foreach (var currentEntity in hierarchy.Entities)
            {
                var transformationComponent = currentEntity.Get(TransformationComponent.Key);
                if (transformationComponent == null)
                    continue;

                if (transformationComponent.Children.Any(x => x.Entity == entity))
                    return currentEntity;
            }

            return null;
        }

        public static void UpdateScene(EntityAsset source, EntityAsset dest, Guid destRootEntityId)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (dest == null) throw new ArgumentNullException("dest");

            EntityBase entityBase;
            if (!dest.AssetBases.TryGetValue(destRootEntityId, out entityBase))
                throw new InvalidOperationException("This entity base was not found with given Id.");

            var entityBaseAsset = (EntityAsset)entityBase.Base.Asset;

            // Extract the scene starting from given root
            var clonedSource = ExtractSceneClone(source, entityBase.SourceRoot);

            // Process entities in mapping
            var entitiesDiff3 = new List<EntityDiff3>();
            var entitiesSourceId = new HashSet<Guid>(entityBase.IdMapping.Values);
            var oppositeMapping = entityBase.IdMapping.ToDictionary(x => x.Value, x => x.Key);
            foreach (var entityIdEntry in entityBase.IdMapping)
            {
                var entityDiff3 = new EntityDiff3();

                var destEntityId = entityIdEntry.Key;
                var sourceEntityId = entityIdEntry.Value;

                // Get entity in dest asset (if not there anymore, we can simply skip them, they have been deleted so they can be ignored from future merges)
                if (!dest.Hierarchy.Entities.TryGetValue(destEntityId, out entityDiff3.Asset1))
                    continue;

                // Get entity in updated asset
                clonedSource.Hierarchy.Entities.TryGetValue(sourceEntityId, out entityDiff3.Asset2);
                
                // Get entity in base (previous import)
                entityBaseAsset.Hierarchy.Entities.TryGetValue(sourceEntityId, out entityDiff3.Base);

                entitiesDiff3.Add(entityDiff3);
            }

            // Merge
            foreach (var entityDiff3 in entitiesDiff3)
            {
                throw new NotImplementedException();
                //entityDiff3.MergeResult = AssetMerge.Merge(entityDiff3.Base, entityDiff3.Asset1, entityDiff3.Asset2, AssetMergePolicies.MergePolicyAsset2AsNewBaseOfAsset1);

                // TODO: Proper logging and error recovery
                if (entityDiff3.MergeResult.HasErrors)
                    throw new InvalidOperationException("Merge error");
            }

            // We gather entities that were added in our source since last import
            // Note: We only cares about the ones that are in source but not in base -- everything else should be in entityBase.IdMapping
            //       (otherwise it means entity has been deleted already in dest and/or source, so merge is deleted)
            var sourceExtraIds = new HashSet<Guid>(clonedSource.Hierarchy.Entities.Select(x => x.Id));  // Everything in source,
            sourceExtraIds.ExceptWith(entityBaseAsset.Hierarchy.Entities.Select(x => x.Id));            // but not in base,
            sourceExtraIds.ExceptWith(entitiesSourceId);                                                // and not in entityBase.IdMapping...

            foreach (var sourceEntityId in sourceExtraIds)
            {
                var entityDiff3 = new EntityDiff3();

                // Get entity in updated asset
                clonedSource.Hierarchy.Entities.TryGetValue(sourceEntityId, out entityDiff3.Asset2);

                // Add it in our new entity, if possible at the same location
                var asset = entityDiff3.Asset2;
                var parentSourceEntity = FindParent(clonedSource.Hierarchy, entityDiff3.Asset2);
                Guid parentDestEntityId;
                if (!oppositeMapping.TryGetValue(parentSourceEntity.Id, out parentDestEntityId))
                    continue;

                
            }

            // Rebuild tree
            foreach (var entityDiff3 in entitiesDiff3)
            {
                // TODO: Try to propagate tree changes (it's not a big deal if we fail, but try to do it as good as possible)
            }
        }

        // TODO: Use Diff3Node?
        class EntityDiff3
        {
            public Entity Base;
            public Entity Asset1;
            public Entity Asset2;
            public MergeResult MergeResult;
        }

        public static EntityHierarchyData ImportScene(UFile sourceUrl, EntityAsset source, Guid sourceRootEntity, out EntityBase entityBase)
        {
            if (source == null) throw new ArgumentNullException("source");

            // Extract the scene starting from given root
            // Note: only extracting root is supported as of now
            var clonedSource = ExtractSceneClone(source, sourceRootEntity);

            var newAsset = (EntityAsset)AssetCloner.Clone(clonedSource);

            // Generate entity mapping
            var entityMapping = new Dictionary<Guid, Guid>();
            var reverseEntityMapping = new Dictionary<Guid, Guid>();
            foreach (var entity in newAsset.Hierarchy.Entities)
            {
                // Generate new Id
                var newEntityId = Guid.NewGuid();

                // Update mappings
                entityMapping.Add(newEntityId, entity.Id);
                reverseEntityMapping.Add(entity.Id, newEntityId);

                // Update entity with new id
                entity.Id = newEntityId;
            }

            // Rewrite entity references
            // Should we nullify invalid references?
            EntityAnalysis.RemapEntitiesId(newAsset.Hierarchy, reverseEntityMapping);

            // Add asset base
            entityBase = new EntityBase { Base = new AssetBase(sourceUrl, clonedSource), SourceRoot = sourceRootEntity, IdMapping = entityMapping };

            return newAsset.Hierarchy;
        }
    }
}