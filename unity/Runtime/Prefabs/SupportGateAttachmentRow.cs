using Hooligapps.SupportGate.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Prefabs
{
    [DisallowMultipleComponent]
    public sealed class SupportGateAttachmentRow : MonoBehaviour
    {
        [SerializeField] private Text _name;
        [SerializeField] private Text _status;
        [SerializeField] private Button _remove;
        [SerializeField] private RawImage _preview;
        [SerializeField] private Text _extension;
        [SerializeField] private CanvasGroup _pending;

        private int _index;
        private bool _fitPending;

        public event Action<int> RemoveRequested;

        private void LateUpdate()
        {
            if (_fitPending && _preview != null && _preview.rectTransform.rect.width > 0f)
            {
                _fitPending = false;
                FitPreview(_preview.texture);
            }
        }

        public void Bind(int index, SupportGateAttachmentItem item, string uploadingText)
        {
            _index = index;

            if (_name != null)
            {
                _name.supportRichText = false;
                _name.text = item.FileName;
            }

            if (_status != null)
            {
                _status.text = item.State == SupportGateUploadState.Uploading
                    ? uploadingText
                    : Size(item.Size);
            }

            if (_remove != null)
            {
                _remove.onClick.RemoveAllListeners();
                _remove.onClick.AddListener(() => RemoveRequested?.Invoke(_index));
            }

            if (_preview != null)
            {
                _preview.texture = item.Preview;
                _preview.gameObject.SetActive(item.Preview != null);

                _fitPending = item.Preview != null;
            }

            if (_extension != null)
            {
                _extension.text = item.Extension;
                _extension.gameObject.SetActive(item.Preview == null);
            }

            if (_pending != null)
            {
                _pending.alpha = item.State == SupportGateUploadState.Uploading ? 0.5f : 1f;
            }
        }

        private void FitPreview(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            var rect = _preview.rectTransform.rect;
            var textureAspect = (float)texture.width / texture.height;
            var frameAspect = rect.width / rect.height;
            if (textureAspect > frameAspect)
            {
                var w = frameAspect / textureAspect;
                _preview.uvRect = new Rect((1f - w) / 2f, 0f, w, 1f);
            }
            else
            {
                var h = textureAspect / frameAspect;
                _preview.uvRect = new Rect(0f, (1f - h) / 2f, 1f, h);
            }
        }

        private static string Size(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return bytes / 1024 + " KB";
            }

            return bytes / 1024 / 1024 + " MB";
        }
    }
}
