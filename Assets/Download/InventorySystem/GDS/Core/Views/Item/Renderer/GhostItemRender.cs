using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core.Views {
    public class GhostItemRender<T> : Component<Item> where T : Component<Item> {
        public GhostItemRender() {
            ItemRender = Activator.CreateInstance<T>();
            this.Add("", ItemRender).IgnorePickAll();
        }

        T ItemRender;
        int CellSize = 64;

        public override void Render(Item item) {
            ItemRender.Data = item;
            // ItemRender.style.width = ItemRender.resolvedStyle.minWidth.value * item.Size().W;
            // ItemRender.style.height = ItemRender.resolvedStyle.minHeight.value * item.Size().H;
            ItemRender.style.width = CellSize * item.Size().W;
            ItemRender.style.height = CellSize * item.Size().H;
        }
    }
}