﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System.Collections.Generic;
using System.Collections.Specialized;

using SiliconStudio.Paradox.Effects;
using SiliconStudio.Paradox.EntityModel;
using SiliconStudio.Paradox.Games;
using SiliconStudio.Core.Extensions;
using SiliconStudio.Core;
using SiliconStudio.Core.Collections;

namespace SiliconStudio.Paradox.Engine
{
    public class ModelProcessor : EntityProcessor<ModelProcessor.AssociatedData>
    {
        private RenderSystem renderSystem;

        /// <summary>
        /// The link transformation to update.
        /// </summary>
        /// <remarks>The collection is declared globally only to avoid allocation at each frames</remarks>
        private FastCollection<TransformationComponent> linkTransformationToUpdate = new FastCollection<TransformationComponent>();

        public ModelProcessor()
            : base(new PropertyKey[] { ModelComponent.Key, TransformationComponent.Key })
        {
        }

        protected internal override void OnSystemAdd()
        {
            renderSystem = Services.GetSafeServiceAs<RenderSystem>();

            foreach (var pipeline in renderSystem.Pipelines)
                RegisterPipelineForEvents(pipeline);

            renderSystem.Pipelines.CollectionChanged += Pipelines_CollectionChanged;
        }

        protected internal override void OnSystemRemove()
        {
            renderSystem.Pipelines.CollectionChanged -= Pipelines_CollectionChanged;

            foreach (var pipeline in renderSystem.Pipelines)
                UnregisterPipelineForEvents(pipeline);
        }

        protected override AssociatedData GenerateAssociatedData(Entity entity)
        {
            return new AssociatedData { ModelComponent = entity.Get(ModelComponent.Key), TransformationComponent = entity.Transform };
        }

        protected override void OnEntityAdding(Entity entity, AssociatedData associatedData)
        {
            associatedData.RenderModels = new List<KeyValuePair<ModelRendererState, RenderModel>>();

            // Initialize a RenderModel for every pipeline
            foreach (var pipeline in renderSystem.Pipelines)
            {
                CreateRenderModel(associatedData, pipeline);
            }
        }

        private static void CreateRenderModel(AssociatedData associatedData, RenderPipeline pipeline)
        {
            var modelInstance = associatedData.ModelComponent;
            var modelRenderState = pipeline.GetOrCreateModelRendererState();

            // If the model is not accepted
            if (modelRenderState.AcceptModel == null || !modelRenderState.AcceptModel(modelInstance))
            {
                return;
            }

            var renderModel = new RenderModel(pipeline, modelInstance);

            // Register RenderModel
            associatedData.RenderModels.Add(new KeyValuePair<ModelRendererState, RenderModel>(modelRenderState, renderModel));
        }

        private static void DestroyRenderModel(AssociatedData associatedData, RenderPipeline pipeline)
        {
            // Not sure if it's worth making RenderModels a Dictionary<RenderPipeline, List<X>>
            // (rationale: reloading of pipeline is probably rare enough and we don't want to add so many objects and slow normal rendering)
            // Probably need to worry only if it comes out in a VTune
            for (int index = 0; index < associatedData.RenderModels.Count; index++)
            {
                var renderModel = associatedData.RenderModels[index];
                if (renderModel.Value.Pipeline == pipeline)
                {
                    DestroyRenderModel(renderModel.Value);
                    associatedData.RenderModels.SwapRemoveAt(index--);
                }
            }
        }

        private static void DestroyRenderModel(RenderModel renderModel)
        {
            // TODO: Unload resources (need opposite of ModelRenderer.PrepareModelForRendering)
            // (not sure if we actually create anything non-managed?)
        }

        private void Pipelines_CollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
        {
            var pipeline = (RenderPipeline)e.Item;

            // Instantiate/destroy render model for every tracked entity
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var matchingEntity in enabledEntities)
                        CreateRenderModel(matchingEntity.Value, pipeline);
                    RegisterPipelineForEvents(pipeline);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    UnregisterPipelineForEvents(pipeline);
                    foreach (var matchingEntity in enabledEntities)
                        DestroyRenderModel(matchingEntity.Value, pipeline);
                    break;
            }
        }

        private void RegisterPipelineForEvents(RenderPipeline pipeline)
        {
            pipeline.GetOrCreateModelRendererState().ModelSlotAdded += MeshProcessor_ModelSlotAdded;
            pipeline.GetOrCreateModelRendererState().ModelSlotRemoved += MeshProcessor_ModelSlotRemoved;
        }

        private void UnregisterPipelineForEvents(RenderPipeline pipeline)
        {
            pipeline.GetOrCreateModelRendererState().ModelSlotAdded -= MeshProcessor_ModelSlotAdded;
            pipeline.GetOrCreateModelRendererState().ModelSlotRemoved -= MeshProcessor_ModelSlotRemoved;
        }

        void MeshProcessor_ModelSlotAdded(ModelRendererState modelRendererState, ModelRendererSlot modelRendererSlot)
        {
            foreach (var matchingEntity in enabledEntities)
            {
                // Look for existing render model
                RenderModel renderModel = null;
                foreach (var renderModelKVP in matchingEntity.Value.RenderModels)
                {
                    if (renderModelKVP.Key == modelRendererState)
                    {
                        renderModel = renderModelKVP.Value;
                        break;
                    }
                }

                // If it doesn't exist, it's because it wasn't accepted
                if (renderModel == null)
                    continue;

                // Ensure RenderMeshes is big enough to contain the new slot
                renderModel.RenderMeshes.EnsureCapacity(modelRendererSlot.Slot + 1);

                // Prepare the render meshes
                modelRendererSlot.PrepareRenderModel(renderModel);
            }
        }

        void MeshProcessor_ModelSlotRemoved(ModelRendererState modelRendererState, ModelRendererSlot modelRendererSlot)
        {
            foreach (var matchingEntity in enabledEntities)
            {
                // Look for existing render model
                RenderModel renderModel = null;
                foreach (var renderModelKVP in matchingEntity.Value.RenderModels)
                {
                    if (renderModelKVP.Key == modelRendererState)
                    {
                        renderModel = renderModelKVP.Value;
                        break;
                    }
                }

                // If it doesn't exist, it's because it wasn't accepted
                if (renderModel == null)
                    continue;

                // Remove the slot
                // TODO: Free resources?
                renderModel.RenderMeshes[modelRendererSlot.Slot] = null;
            }
        }

        protected override void OnEntityRemoved(Entity entity, AssociatedData data)
        {
            foreach (var renderModel in data.RenderModels)
            {
                DestroyRenderModel(renderModel.Value);
            }

            base.OnEntityRemoved(entity, data);
        }

        public EntityLink LinkEntity(Entity linkedEntity, ModelComponent modelComponent, string boneName)
        {
            var modelEntityData = matchingEntities[modelComponent.Entity];
            var nodeIndex = modelEntityData.ModelComponent.ModelViewHierarchy.Nodes.IndexOf(x => x.Name == boneName);

            var entityLink = new EntityLink { Entity = linkedEntity, ModelComponent = modelComponent, NodeIndex = nodeIndex };
            if (nodeIndex == -1)
                return entityLink;

            linkedEntity.Transform.isSpecialRoot = true;
            linkedEntity.Transform.UseTRS = false;

            if (modelEntityData.Links == null)
                modelEntityData.Links = new List<EntityLink>();

            modelEntityData.Links.Add(entityLink);

            return entityLink;
        }

        public bool UnlinkEntity(EntityLink entityLink)
        {
            if (entityLink.NodeIndex == -1)
                return false;

            AssociatedData modelEntityData;
            if (!matchingEntities.TryGetValue(entityLink.ModelComponent.Entity, out modelEntityData))
                return false;

            return modelEntityData.Links.Remove(entityLink);
        }

        public override void Draw(GameTime time)
        {
            // Clear all pipelines from previously collected models
            foreach (var pipeline in renderSystem.Pipelines)
            {
                var renderMeshState = pipeline.GetOrCreateModelRendererState();
                renderMeshState.RenderModels.Clear();
            }

            // Collect models for this frame
            foreach (var matchingEntity in enabledEntities)
            {
                // Skip disabled model components, or model components without a proper model set
                if (!matchingEntity.Value.ModelComponent.Enabled || matchingEntity.Value.ModelComponent.ModelViewHierarchy == null)
                {
                    continue;
                }

                var modelViewHierarchy = matchingEntity.Value.ModelComponent.ModelViewHierarchy;

                var transformationComponent = matchingEntity.Value.TransformationComponent;

                var links = matchingEntity.Value.Links;

                // Update model view hierarchy node matrices
                modelViewHierarchy.NodeTransformations[0].LocalMatrix = transformationComponent.WorldMatrix;
                modelViewHierarchy.UpdateMatrices();

                if (links != null)
                {
                    // Update links: transfer node/bone transformation to a specific entity transformation
                    // Then update this entity transformation tree
                    // TODO: Ideally, we should order update (matchingEntities?) to avoid updating a ModelViewHierarchy before its transformation is updated.
                    foreach (var link in matchingEntity.Value.Links)
                    {
                        var linkTransformation = link.Entity.Transform;
                        linkTransformation.LocalMatrix = modelViewHierarchy.NodeTransformations[link.NodeIndex].WorldMatrix;

                        linkTransformationToUpdate.Clear();
                        linkTransformationToUpdate.Add(linkTransformation);
                        TransformationProcessor.UpdateTransformations(linkTransformationToUpdate, false);
                    }
                }

                foreach (var renderModelEntry in matchingEntity.Value.RenderModels)
                {
                    var renderModelState = renderModelEntry.Key;
                    var renderModel = renderModelEntry.Value;

                    // Add model to rendering
                    renderModelState.RenderModels.Add(renderModel);

                    // Upload matrices to TransformationKeys.World
                    modelViewHierarchy.UpdateToRenderModel(renderModel);

                    // Upload skinning blend matrices
                    MeshSkinningUpdater.Update(modelViewHierarchy, renderModel);
                }
            }
        }

        public class AssociatedData
        {
            public ModelComponent ModelComponent;

            public TransformationComponent TransformationComponent;

            internal List<KeyValuePair<ModelRendererState, RenderModel>> RenderModels;

            public List<EntityLink> Links;
        }

        public struct EntityLink
        {
            public int NodeIndex;
            public Entity Entity;
            public ModelComponent ModelComponent;
        }
    }
}