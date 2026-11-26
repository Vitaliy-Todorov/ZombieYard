// Ответственности
// - Показывает состояние игрока, оружия и босса, подсказки и результат попытки.
// - Передаёт игроку команды из меню паузы и финального экрана.

using UnityEngine;

namespace ZombieYard
{
    [RequireComponent(typeof(YardPlayer))]
    public sealed class YardHud : MonoBehaviour
    {
        internal ExteriorFog Fog { private get; set; }
        internal ArenaEye Boss { private get; set; }
        private YardPlayer player;
        private GUIStyle label, button;
        private static readonly Color Ink = new(.065f, .09f, .085f, .94f);
        private static readonly Color Paper = new(.94f, .93f, .82f);
        private static readonly Color Amber = new(1f, .66f, .20f);
        private static readonly Color Muted = new(.68f, .74f, .65f);
        private static readonly Color Rust = new(.90f, .32f, .18f);
        private void Awake() => player = GetComponent<YardPlayer>();

        private static void Fill(float x, float y, float w, float h, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void Text(float x, float y, float w, float h, string value, int size, Color color, bool bold = false, bool center = false)
        {
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            label.normal.textColor = color;
            GUI.Label(new Rect(x, y, w, h), value, label);
        }

        private void OnGUI()
        {
            if (player.Gear == null) return;
            label ??= new GUIStyle(GUI.skin.label) { padding = new RectOffset() };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 600f);
            float w = Screen.width / scale, h = Screen.height / scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var hp = player.Health;
            var gear = player.Gear;
            string time = System.TimeSpan.FromSeconds(player.Elapsed).ToString(@"mm\:ss");
            Fill(22, 22, 218, 85, Ink);
            Fill(22, 22, 4, 85, Amber);
            Text(40, 28, 195, 24, player.Cycle == 0 ? "ZOMBIE YARD" : $"ZOMBIE YARD · NG+{player.Cycle}", 18, Paper, true);
            Text(40, 54, 185, 20, time, 13, Muted, true);
            Text(40, 77, 190, 23, "Счёт   " + player.Score.Balance, 16, Amber, true);

            Fill(22, h - 82, 183, 60, Ink);
            Text(36, h - 75, 95, 24, "ЗДОРОВЬЕ", 13, Muted, true);
            Text(144, h - 77, 58, 28, hp.Current.ToString(), 23, Paper, true);
            Fill(36, h - 43, 154, 7, new Color(.24f,.27f,.23f));
            Fill(36, h - 43, 154f * hp.Current / hp.Maximum, 7, hp.Current > hp.Maximum / 3 ? Amber : Rust);

            float slotWidth = Mathf.Min(122, (w - 258) / gear.Slots.Count);
            for (int i = 0; i < gear.Slots.Count; i++)
            {
                float x = w - 22 - (gear.Slots.Count - i) * slotWidth;
                bool selected = gear.Selected == i;
                Fill(x, h - 69, slotWidth - 3, 47, Ink);
                if (selected) Fill(x, h - 69, slotWidth - 3, 3, Amber);
                Text(x + 9, h - 59, 20, 28, (i + 1).ToString(), 13, selected ? Amber : Muted, true);
                Text(x + 29, h - 59, slotWidth - 32, 28, (gear.Slots[i]?.Config ?? player.config.fists).title, 15, Paper, selected);
            }
            if (gear.Active.Config.UsesAmmo)
            {
                string rounds = gear.Reloading ? "ПЕРЕЗАРЯДКА" : gear.Active.Loaded + "  /  " + gear.Stock(gear.Active.Ammo);
                Fill(w - 169, h - 107, 144, 33, Ink);
                Text(w - 169, h - 106, 144, 32, rounds, 16, Amber, true, true);
            }
            if (hp.Current > 0 && !player.Paused)
            {
                float x = w / 2, y = h / 2;
                if (Boss && Boss.InAttackRange)
                {
                    var bossHP = Boss.Health;
                    Fill(x - 175, 22, 350, 49, Ink);
                    Text(x - 161, 25, 220, 26, "ГЛАЗ КРАСНОЙ ГОРЫ", 16, Paper, true);
                    Text(x + 55, 25, 108, 26, $"{bossHP.Current} / {bossHP.Maximum}", 15, Paper, true, true);
                    Fill(x - 161, 54, 322, 7, Muted);
                    Fill(x - 161, 54, 322f * bossHP.Current / bossHP.Maximum, 7, Rust);
                }
                Color cross = Time.time < player.HitUntil ? Amber : Paper;
                Fill(x - 7, y, 4, 2, cross); Fill(x + 4, y, 4, 2, cross);
                Fill(x, y - 7, 2, 4, cross); Fill(x, y + 4, 2, 4, cross);
                if (Fog && Fog.Exposed) Text(x - 175, y - 70, 350, 30, "Опасный туман · поднимитесь выше", 17, Amber, true, true);
                string prompt = player.Notice ?? (player.AimedStand ? player.AimedStand.Prompt :
                    player.Nearby ? "E   /   Подобрать · " + player.Nearby.Title : null);
                if (prompt != null)
                {
                    Fill(x - 250, y + 28, 500, 32, Ink);
                    Text(x - 250, y + 28, 500, 32, prompt, 15, Paper, true, true);
                }
                if (player.Elapsed < 14)
                {
                    Fill(w / 2 - 292, h - 128, 584, 36, Ink);
                    Text(w / 2 - 292, h - 128, 584, 36, "1 / 2  Слот     ЛКМ  Атака     E  Подобрать     G  Выбросить     R  Зарядить     Esc  Помощь", 13, Paper, false, true);
                }
                if (Time.time < player.HurtUntil) Fill(0, 0, w, h, new Color(.86f,.12f,.03f, (player.HurtUntil - Time.time) * .75f));
            }
            else
            {
                Fill(0, 0, w, h, new Color(.02f,.04f,.035f,.70f));
                float x = w / 2 - 216, y = h / 2 - 111;
                Fill(x, y, 432, 222, Ink); Fill(x, y, 432, 4, Amber);
                string title = player.Victory ? "Глаз повержен" : hp.Current == 0 ? "Попытка окончена" : "Пауза";
                Text(x, y + 16, 432, 48, title, 30, Paper, true, true);
                if (hp.Current == 0 || player.Victory)
                {
                    Text(x, y + 76, 432, 32, "Время  " + time + "     /     Убито  " + player.Kills, 19, Muted, false, true);
                    var score = player.Score;
                    Text(x, y + 111, 432, 25, $"Обычные  {score.Normal}    Красные  {score.Red}    Фиолетовые  {score.Purple}", 16, Paper, false, true);
                    string action = player.Victory ? $"Начать NG+{player.Cycle + 1}" : "Ещё попытка   [R]";
                    if (GUI.Button(new Rect(x + 86, y + 159, 260, 42), action, button))
                    {
                        if (player.Victory) player.ConfirmNewGamePlus();
                        else player.Restart();
                    }
                }
                else
                {
                    Text(x, y + 66, 432, 25, "WASD — ходьба   ·   Shift — бег   ·   Пробел — прыжок", 15, Paper, false, true);
                    Text(x, y + 93, 432, 25, "ЛКМ — атака   ·   R — зарядить   ·   E — взять оружие", 15, Paper, false, true);
                    Text(x, y + 120, 432, 25, "1–2 или колесо — слоты   ·   G — бросить оружие", 15, Paper, false, true);
                    if (GUI.Button(new Rect(x + 12, y + 159, 210, 42), "Продолжить   [Esc]", button))
                        player.TogglePause();
                    if (GUI.Button(new Rect(x + 230, y + 159, 190, 42), "Перезапустить", button))
                        player.Restart();
                }
            }
            GUI.matrix = previous;
        }
    }
}
