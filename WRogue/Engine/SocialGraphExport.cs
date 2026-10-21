using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    [DataContract]
    sealed class SocialGraphDocument
    {
        [DataMember] public int schema = 1;
        [DataMember] public int turn;
        [DataMember] public bool partial;
        [DataMember] public List<SocialGraphNode> nodes = new List<SocialGraphNode>();
        [DataMember] public List<SocialGraphEdge> edges = new List<SocialGraphEdge>();
        [DataMember] public List<SocialGraphResident> residents = new List<SocialGraphResident>();
    }
    [DataContract]
    sealed class SocialGraphNode
    {
        [DataMember] public string id, type, label, detail;
        public SocialGraphNode(string id, string type, string label, string detail)
        { this.id = id; this.type = type; this.label = label; this.detail = detail; }
    }
    [DataContract]
    sealed class SocialGraphEdge
    {
        [DataMember] public string from, to, label;
        public SocialGraphEdge(string from, string to, string label)
        { this.from = from; this.to = to; this.label = label; }
    }
    [DataContract]
    sealed class SocialGraphResident
    {
        [DataMember] public string id, name, faction, group;
        [DataMember] public int spawn, death, interestScore;
        [DataMember] public List<SocialGraphEntry> entries = new List<SocialGraphEntry>();
    }
    [DataContract]
    sealed class SocialGraphEntry
    {
        [DataMember] public int turn, sequence, categories;
        [DataMember] public string kind, text, subject, other, story;
        [DataMember] public long eventId, causeId;
        [DataMember] public long[] supporting;
        [DataMember] public bool direct;
        [DataMember] public int[] axesAfter;
    }

    // Export the same archive section as Read Records; no session restore or game mutation.
    static class SocialGraphExport
    {
        public static void Write(string savePath, string outputPath)
        {
            var document = Build(savePath == null ? null : RecordsReader.Load(savePath));
            using (var file = File.Create(outputPath))
            {
                bool script = outputPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
                if (script)
                {
                    byte[] prefix = Encoding.UTF8.GetBytes("window.SOCIAL_GRAPH_DATA=");
                    file.Write(prefix, 0, prefix.Length);
                }
                new DataContractJsonSerializer(typeof(SocialGraphDocument),
                    new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = Int32.MaxValue })
                    .WriteObject(file, document);
                if (script) file.WriteByte((byte)';');
            }
        }

        internal static SocialGraphDocument Build(RecordsSave save)
        {
            var result = new SocialGraphDocument();
            AddCatalog(result, NpcContentCatalog.Default);
            if (save == null) return result;
            result.turn = save.Turn;
            result.partial = save.Records.IsPartial;
            foreach (ResidentRecord resident in RecordsReader.Residents(save))
            {
                var person = new SocialGraphResident { id = resident.Identity.ToString("D"),
                    name = resident.Name, faction = resident.FactionName, group = resident.GroupName,
                    spawn = resident.SpawnTurn, death = resident.DeathTurn,
                    interestScore = new RecordsProfile(resident, save.Turn).Score };
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Turn <= save.Turn)
                        person.entries.Add(new SocialGraphEntry { turn = entry.Turn,
                            sequence = entry.Sequence, categories = entry.RecordCategories,
                            kind = entry.Kind, text = RecordsReader.DisplayText(entry),
                            subject = entry.SubjectId == Guid.Empty ? null : entry.SubjectId.ToString("D"),
                            other = entry.OtherId == Guid.Empty ? null : entry.OtherId.ToString("D"),
                            eventId = entry.EventId, causeId = entry.CauseId, story = entry.StoryId,
                            supporting = entry.SupportingCauses, direct = entry.Direct,
                            axesAfter = entry.AxesAfter });
                result.residents.Add(person);
            }
            return result;
        }

        static void Node(SocialGraphDocument doc, string id, string type, string label, string detail)
        { doc.nodes.Add(new SocialGraphNode(id, type, label, detail)); }
        static void Edge(SocialGraphDocument doc, string from, string to, string label)
        { doc.edges.Add(new SocialGraphEdge(from, to, label)); }
        static string Handler(Delegate action)
        {
            Type type = action.Method.DeclaringType;
            while (type.DeclaringType != null && type.Name.StartsWith("<", StringComparison.Ordinal))
                type = type.DeclaringType;
            string method = action.Method.Name;
            return type.Name + (method.StartsWith("<", StringComparison.Ordinal) ? "" : "." + method);
        }
        static string PhaseName(NpcObservationPhase phase)
        {
            switch (phase)
            {
                case NpcObservationPhase.Knowledge: return "Знание";
                case NpcObservationPhase.Relationships: return "Отношения";
                case NpcObservationPhase.Goals: return "Цель";
                case NpcObservationPhase.Responses: return "Быстрый ответ";
                default: return "Реплика";
            }
        }
        static void HandlerEdge(SocialGraphDocument doc, NpcEventDefinition ev, Delegate action,
            string phase, string stage, int index)
        {
            string id = "handler:" + ev.Id + ":" + stage + ":" + index;
            string source = Handler(action);
            string label = phase + ": " + ev.Id.Replace('_', ' ') + " · " + source;
            if (action.Method.Name.StartsWith("<", StringComparison.Ordinal) && index > 0)
                label += " #" + (index + 1);
            Node(doc, id, "handler", label,
                "Событие: " + ev.Id + "\nЭтап: " + phase + "\nКод: " + Handler(action) +
                "\nУсловия и возможный результат определяются правилом модуля.");
            Edge(doc, "event:" + ev.Id, id, phase);
            Edge(doc, id, "flow:" + stage, "исполняется на этапе");
        }

        static void AddFlow(SocialGraphDocument doc)
        {
            string[,] stages = {
                { "action", "Исполненное действие", "Результат реального действия в игровом мире. Неудачная попытка не публикует социальное последствие." },
                { "event", "Значимое событие", "SignificantEvent с участниками, местом, временем, причиной и историей." },
                { "audience", "Аудитория", "Участники и свидетели по видимости и контракту события." },
                { "knowledge", "Личное знание", "Наблюдение меняет известные факты и людей только у допущенного NPC." },
                { "relationships", "Отношения", "Личные, групповые и фракционные оценки обрабатываются отдельно." },
                { "goals", "Цели", "Потребности и известные факты участвуют в выборе длительной цели." },
                { "responses", "Быстрый ответ", "Краткий отклик на наблюдаемое действие; его исполнение проверяется отдельно." },
                { "replies", "Реплика", "Словесный ответ на услышанную просьбу или событие." },
                { "rumor", "Слух", "RetainFact сохраняет факт; слушатель оценивает источник и уверенность без памяти очевидца." },
                { "after-event", "После события", "Обработчик AfterEvent исполняется после наблюдателей; его возможный результат зависит от правила модуля." },
                { "emotion", "Эмоциональный отклик", "Непосредственное событие или радиослух может изменить состояние наблюдателя." },
                { "archive", "Read Records", "Сохранённая хронология конкретного жителя; не равна его текущим числовым отношениям." },
                { "memory", "Память", "Наблюдённое событие может создать память, влияющую на отношения и поздний исход." },
                { "reaction", "Исполнение реакции", "Очередная NpcReaction становится действием только после проверки возможности исполнения." }
            };
            for (int i = 0; i < stages.GetLength(0); i++)
                Node(doc, "flow:" + stages[i, 0], "flow", stages[i, 1], stages[i, 2]);
            Edge(doc, "flow:action", "flow:event", "после успеха");
            Edge(doc, "flow:event", "flow:audience", "определяет");
            Edge(doc, "flow:audience", "flow:knowledge", "наблюдает");
            Edge(doc, "flow:audience", "flow:relationships", "оценивает");
            Edge(doc, "flow:audience", "flow:goals", "побуждает");
            Edge(doc, "flow:audience", "flow:responses", "побуждает");
            Edge(doc, "flow:audience", "flow:replies", "побуждает");
            Edge(doc, "flow:audience", "flow:memory", "может запомнить");
            Edge(doc, "flow:audience", "flow:archive", "записывает");
            Edge(doc, "flow:event", "flow:rumor", "если факт сохраняется");
            Edge(doc, "flow:event", "flow:after-event", "после наблюдателей");
            Edge(doc, "flow:audience", "flow:emotion", "может повлиять");
            Edge(doc, "flow:rumor", "flow:emotion", "радио может повлиять");
            Edge(doc, "flow:rumor", "flow:knowledge", "доставляет слушателю");
            Edge(doc, "flow:responses", "flow:reaction", "ставит в очередь");
            Edge(doc, "flow:replies", "flow:reaction", "ставит в очередь");
            Edge(doc, "flow:reaction", "flow:action", "при законном исполнении");
            Edge(doc, "flow:goals", "composer", "строит план");
        }

        static void AddCatalog(SocialGraphDocument doc, NpcContentCatalog catalog)
        {
            Node(doc, "composer", "composer", "Планировщик", "Цель выбирается по текущим потребностям и знаниям. Шаги подбираются по доступным операторам и фактам.");
            AddFlow(doc);
            foreach (NpcValueDefinition value in catalog.AllValues)
            {
                string id = "value:" + value.Id;
                Node(doc, id, "value", value.Id, value.Description);
                foreach (string capability in value.EquivalentCapabilities)
                    Edge(doc, id, "capability:" + capability, "достигается целью");
            }
            foreach (NpcIntentDefinition goal in catalog.AllCapabilities)
            {
                string id = "capability:" + goal.Id;
                Node(doc, id, "plan", goal.Name, "ID: " + goal.Id + "\nСрок: " + goal.Duration + "; пауза: " + goal.Cooldown +
                    "\nПриоритет: " + goal.BasePriority + "; порог: " + goal.Threshold +
                    (goal.Resource == null ? "" : "\nРесурс: " + goal.Resource) +
                    (goal.ResultFacts != null ? "\nРезультат: факты планировщика" : goal.Result != null ? "\nРезультат: условие планировщика" : ""));
                Edge(doc, id, "composer", "планируется");
                Edge(doc, "flow:goals", id, "может выбрать");
                if (goal.Resource != null) Edge(doc, id, "resource:" + goal.Resource, "нужен ресурс");
            }
            foreach (NpcResourceDefinition resource in catalog.AllResources)
                Node(doc, "resource:" + resource.Id, "resource", resource.Id, "Оценка личной потребности: " + Handler(resource.OwnNeed));
            foreach (NpcOperatorDefinition op in catalog.AllOperators)
            {
                string id = "operator:" + op.Id;
                Node(doc, id, "operator", op.Id, "Исполнение: " + Handler(op.Execute) +
                    (op.Resource == null ? "" : "\nРесурс: " + op.Resource));
                Edge(doc, "composer", id, "доступный шаг");
                if (op.Resource != null) Edge(doc, id, "resource:" + op.Resource, "использует");
            }
            foreach (NpcOperatorSource source in catalog.OperatorSources)
            {
                string id = "source:" + source.Id;
                Node(doc, id, "source", source.Id, "Привязка: " + Handler(source.Bind));
                Edge(doc, "composer", id, "поставщик шагов");
                foreach (string fact in catalog.Facts.Names(source.Produces(catalog)))
                { EnsureFact(doc, fact); Edge(doc, id, "fact:" + fact, "создаёт"); }
                if (source.Requires != null)
                    foreach (string fact in catalog.Facts.Names(source.Requires(catalog)))
                    { EnsureFact(doc, fact); Edge(doc, "fact:" + fact, id, "требуется"); }
            }
            foreach (NpcInterestDefinition interest in catalog.Interests)
                Node(doc, "interest:" + interest.Id, "interest", interest.Id,
                    "Наблюдение: " + Handler(interest.Observe) + "\nОценка цели: " + Handler(interest.Evaluate));
            foreach (NpcCollectiveDefinition collective in catalog.Collectives)
            {
                string id = "collective:" + collective.Id;
                Node(doc, id, "collective", collective.Id,
                    "Область: " + collective.Scope + "\nПредложение: " + Handler(collective.Propose) +
                    "\nПринятие: " + Handler(collective.Accept));
                Edge(doc, id, "event:" + collective.EventId, "объявляется событием");
                if (collective.CoordinatorCapability != null)
                    Edge(doc, id, "capability:" + collective.CoordinatorCapability, "координация");
            }
            foreach (NpcEventDefinition ev in catalog.AllEvents)
            {
                string id = "event:" + ev.Id;
                Node(doc, id, "event", ev.Id.Replace('_', ' '),
                    "ID: " + ev.Id + "\nКатегории: " + ev.Categories +
                    "\nСохраняется как факт: " + (ev.RetainFact ? "да" : "нет") +
                    "; приватное: " + (ev.Private ? "да" : "нет") +
                    "\nОписание записи: " + (ev.Describe == null ? "обычное" : Handler(ev.Describe)) +
                    "\nОписание слуха: " + (ev.DescribeReport == null ? "обычное" : Handler(ev.DescribeReport)));
                Edge(doc, "flow:event", id, "вид события");
                Edge(doc, id, "flow:archive", "наблюдения сохраняются");
                if (ev.RetainFact) Edge(doc, id, "flow:rumor", "факт для слуха");
                foreach (NpcObservationPhase phase in Enum.GetValues(typeof(NpcObservationPhase)))
                {
                    int index = 0;
                    foreach (Delegate action in ev.ObservationHandlers(phase))
                        HandlerEdge(doc, ev, action, PhaseName(phase), phase.ToString().ToLowerInvariant(), index++);
                }
                int reportIndex = 0;
                foreach (Delegate action in ev.ReportHandlers)
                    HandlerEdge(doc, ev, action, "Слух", "rumor", reportIndex++);
                int completionIndex = 0;
                foreach (Delegate action in ev.CompletionHandlers)
                    HandlerEdge(doc, ev, action, "После события", "after-event", completionIndex++);
                if (ev.ImmediateSanity != null) HandlerEdge(doc, ev, ev.ImmediateSanity, "Эмоция", "emotion", 0);
                if (ev.RadioSanity != null) HandlerEdge(doc, ev, ev.RadioSanity, "Радиоэмоция", "emotion", 1);
                if (ev.PlayerReply != null)
                {
                    Edge(doc, id, "event:" + ev.PlayerReply.YesKind, "ответ да: " + ev.PlayerReply.YesText);
                    Edge(doc, id, "event:" + ev.PlayerReply.NoKind, "ответ нет: " + ev.PlayerReply.NoText);
                }
                if (ev.ReportDisputesKinds != null)
                    foreach (string kind in ev.ReportDisputesKinds)
                        Edge(doc, id, "event:" + kind, "оспаривает слух");
            }
            foreach (TraitDefinition trait in catalog.Personalities.AllTraits)
            {
                string id = "trait:" + trait.Id;
                Node(doc, id, "trait", trait.Name, "ID: " + trait.Id +
                    "\nТолько приобретённая: " + (trait.EarnedOnly ? "да" : "нет") +
                    (trait.RelationFactionId < 0 ? "" : "\nФракция " + trait.RelationFactionId + ": " + trait.RelationBias));
                if (trait.RequiresTrait != null) Edge(doc, "trait:" + trait.RequiresTrait, id, "предпосылка");
                foreach (TraitEffect effect in trait.Effects)
                {
                    string axis = "axis:" + effect.Axis;
                    EnsureAxis(doc, effect.Axis.ToString());
                    Edge(doc, id, axis, "влияние " + effect.Amount);
                }
            }
            foreach (MemoryDefinition memory in catalog.Personalities.AllMemories)
            {
                string id = "memory:" + memory.Id;
                Node(doc, id, "memory", memory.Name,
                    "ID: " + memory.Id + "\nСозревает через " + memory.MinDays + "–" + memory.MaxDays + " дней" +
                    "\nОтношение: " + memory.PersonRole + " " + memory.FeelingChange +
                    (memory.DispositionAxis.HasValue ? "\nШкала: " + memory.DispositionAxis + " " + memory.DispositionChange : ""));
                if (memory.DispositionAxis.HasValue)
                {
                    EnsureAxis(doc, memory.DispositionAxis.Value.ToString());
                    Edge(doc, id, "axis:" + memory.DispositionAxis.Value,
                        "возможный исход " + memory.DispositionChange);
                }
                foreach (MemoryTrigger trigger in memory.Triggers)
                    Edge(doc, "event:" + trigger.EventKind, id, "может вызвать память");
                foreach (string kind in memory.EvidenceKinds)
                    Edge(doc, "event:" + kind, id, "свидетельство");
                foreach (MemoryOutcome outcome in memory.Outcomes)
                {
                    if (outcome.TraitId != null) Edge(doc, id, "trait:" + outcome.TraitId, "возможный исход");
                    if (outcome.SkillId.HasValue)
                    {
                        string skill = outcome.SkillId.Value.ToString();
                        if (!doc.nodes.Exists(n => n.id == "skill:" + skill))
                            Node(doc, "skill:" + skill, "skill", skill, "Навык, который может измениться при разрешении памяти.");
                        Edge(doc, id, "skill:" + skill, "возможный исход");
                    }
                }
            }
        }
        static void EnsureFact(SocialGraphDocument doc, string fact)
        { if (!doc.nodes.Exists(n => n.id == "fact:" + fact)) Node(doc, "fact:" + fact, "fact", fact, "Факт планировщика."); }
        static void EnsureAxis(SocialGraphDocument doc, string axis)
        { if (!doc.nodes.Exists(n => n.id == "axis:" + axis)) Node(doc, "axis:" + axis, "axis", axis, "Ось личности, влияющая на оценку и выбор."); }
    }
}
