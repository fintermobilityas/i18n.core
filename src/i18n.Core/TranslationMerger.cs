﻿using System;
using System.Collections.Generic;
using System.Linq;
using i18n.Core.Pot;
using i18n.Core.Pot.Entities;

namespace i18n.Core
{
    internal interface ITranslationMerger
    {
        void MergeTranslation(IDictionary<string, TemplateItem> src, Translation dst);
        void MergeAllTranslation(IDictionary<string, TemplateItem> items);
    }

    internal class TranslationMerger : ITranslationMerger
    {
        readonly ITranslationRepository _repository;

        public TranslationMerger(ITranslationRepository repository)
        {
            _repository = repository;
        }

        public void MergeTranslation(IDictionary<string, TemplateItem> src, Translation dst)
        {
            // Our purpose here is to merge newly parsed message items (src) with those already stored in a translation repo (dst).
            // 1. Where an orphan msgid is found (present in the dst but not the src) we update it in the dst to remove all references.
            // 2. Where a src msgid is missing from dst, we simply ADD it to dst.
            // 3. Where a src msgid is present in dst, we update the item in the dst to match the src (references, comments, etc.).
            //
            // 1.
            // Simply remove all references from dst items, for now.
            foreach (var dstItem in dst.Items.Values)
            {
                dstItem.References = null;
            }
            // 2. and 3.
            foreach (var srcItem in src.Values)
            {
                var dstItem = dst.Items.GetOrAdd(srcItem.MsgKey, k => new TranslationItem { MsgKey = srcItem.MsgKey });
                dstItem.MsgId = srcItem.MsgId;
                dstItem.References = srcItem.References;
                dstItem.ExtractedComments = srcItem.Comments;
            }
            // Persist changes.
            _repository.SaveTranslation(dst);
        }

        public void MergeAllTranslation(IDictionary<string, TemplateItem> items)
        {
            foreach (var language in _repository.GetAvailableLanguages())
            {
                var filesNames = items.GroupBy(x => x.Value.FileName).Select(x => x.Key).ToList();
                MergeTranslation(items, _repository.GetTranslation(language.LanguageShortTag, filesNames, false));
            }
        }

    }
}
