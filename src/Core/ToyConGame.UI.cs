using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ToyConEngine
{
    public partial class ToyConGame
    {
        private bool UpdateUI(MouseState mouseState)
        {
            _uiBarRect.Width = Window.ClientBounds.Width;
            _uiBarRect.Height = UiBarHeight;
            bool clicked = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            Point mousePos = mouseState.Position;
            bool captured = false;
            var menus = _menus;
            if (menus is null) return false;

            if (_uiBarRect.Contains(mousePos)) captured = true;

            int buttonWidth = Math.Max(70, Math.Min(100, (Math.Max(200, Window.ClientBounds.Width - 20)) / Math.Max(1, menus.Count)));
            int x = 10;

            if (clicked)
            {
                bool menuClicked = false;
                foreach (var category in menus.Keys)
                {
                    Rectangle btnRect = new Rectangle(x, 0, buttonWidth, UiBarHeight);
                    if (btnRect.Contains(mousePos))
                    {
                        _activeMenu = (_activeMenu == category) ? null : category;
                        menuClicked = true;
                        captured = true;
                        break;
                    }
                    x += buttonWidth + 4;
                }

                if (!menuClicked && _activeMenu != null)
                {
                    int menuX = 10;
                    foreach (var category in menus.Keys)
                    {
                        if (category == _activeMenu) break;
                        menuX += buttonWidth + 4;
                    }

                    if (menus.TryGetValue(_activeMenu, out var items))
                    {
                        int y = UiBarHeight;
                        int itemWidth = Math.Min(180, Math.Max(120, Window.ClientBounds.Width - 20));
                        int itemHeight = 44;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Rectangle itemRect = new Rectangle(menuX, y, itemWidth, itemHeight);
                            if (itemRect.Contains(mousePos))
                            {
                                var n = items[i].Factory();
                                if (n != null) SpawnNode(n);
                                _activeMenu = null;
                                captured = true;
                                menuClicked = true;
                                break;
                            }
                            y += itemHeight;
                        }
                    }
                    if (!menuClicked && !_uiBarRect.Contains(mousePos)) _activeMenu = null;
                }
                else if (!menuClicked && _activeMenu != null && !_uiBarRect.Contains(mousePos)) _activeMenu = null;
            }

            if (_showBenchmarkPopup && _font != null)
            {
                Vector2 sz = _font.MeasureString(_benchmarkResult);
                Vector2 pos = new Vector2((ClientBounds.Width - sz.X) / 2, 50);
                Rectangle popupRect = new Rectangle((int)pos.X - 10, (int)pos.Y - 10, (int)sz.X + 20, (int)sz.Y + 20);
                Rectangle closeRect = new Rectangle(popupRect.Right - 20, popupRect.Top, 20, 20);
                if (clicked && closeRect.Contains(mousePos))
                {
                    _showBenchmarkPopup = false;
                    captured = true;
                }
            }
            return captured;
        }

        private void DrawUI()
        {
            _spriteBatch.Draw(_pixel, _uiBarRect, new Color(40, 40, 40));
            DrawHollowRect(_spriteBatch, _uiBarRect, Color.Gray);
            var menus = _menus;
            if (menus is null) return;

            int buttonWidth = Math.Max(70, Math.Min(100, (Math.Max(200, Window.ClientBounds.Width - 20)) / Math.Max(1, menus.Count)));
            int x = 10;
            foreach (var category in menus.Keys)
            {
                Rectangle btnRect = new Rectangle(x, 0, buttonWidth, UiBarHeight);
                _spriteBatch.Draw(_pixel, btnRect, (_activeMenu == category) ? Color.Gray : Color.DarkGray);
                DrawHollowRect(_spriteBatch, btnRect, Color.White);
                if (_font != null) _spriteBatch.DrawString(_font, category, new Vector2(x + 10, 5), Color.White);

                if (_activeMenu == category)
                {
                    var items = menus[category];
                    int y = UiBarHeight;
                    int itemWidth = Math.Min(280, Math.Max(180, Window.ClientBounds.Width - 20));
                    int itemHeight = 44;
                    for (int i = 0; i < items.Count; i++)
                    {
                        Rectangle itemRect = new Rectangle(x, y, itemWidth, itemHeight);
                        _spriteBatch.Draw(_pixel, itemRect, new Color(50, 50, 50));
                        DrawHollowRect(_spriteBatch, itemRect, Color.LightGray);
                        if (_font != null)
                        {
                            var mousePos = Mouse.GetState().Position;
                            Rectangle questionHoverRect = new Rectangle(itemRect.Right - 30, y, 30, 30);
                            bool isHoveringHelp = questionHoverRect.Contains(mousePos);

                            _spriteBatch.DrawString(_font, items[i].Name, new Vector2(x + 5, y + 4), Color.White);
                            if (isHoveringHelp)
                            {
                                _spriteBatch.DrawString(_font, items[i].Description, new Vector2(x + 5, y + 20), new Color(220, 220, 220));
                            }
                            _spriteBatch.DrawString(_font, "?", new Vector2(itemRect.Right - 16, y + 4), isHoveringHelp ? Color.Yellow : Color.Gray);
                        }
                        y += itemHeight;
                    }
                }
                x += buttonWidth + 4;
            }
        }

        private void DrawTpsGraph(SpriteBatch sb, Rectangle rect)
        {
            if (_tpsHistory.Count < 2) return;

            sb.Draw(_pixel, rect, new Color(0, 0, 0, 100));
            DrawHollowRect(sb, rect, Color.Gray, 1);

            float maxVal = 600000000f;
            foreach (var v in _tpsHistory) if (v > maxVal) maxVal = v;

            float xStep = (float)rect.Width / (MaxTpsHistory - 1);

            for (int i = 0; i < _tpsHistory.Count - 1; i++)
            {
                float v1 = _tpsHistory[i];
                float v2 = _tpsHistory[i + 1];

                Vector2 p1 = new Vector2(rect.X + i * xStep, rect.Bottom - (v1 / maxVal) * rect.Height);
                Vector2 p2 = new Vector2(rect.X + (i + 1) * xStep, rect.Bottom - (v2 / maxVal) * rect.Height);

                DrawLine(sb, p1, p2, Color.Lime, 1);
            }
        }

        private void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness = 2f)
        {
            var edge = end - start;
            var angle = (float)Math.Atan2(edge.Y, edge.X);
            sb.Draw(_pixel, new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness), null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }

        private void DrawHollowRect(SpriteBatch sb, Rectangle rect, Color color, int thickness = 2)
        {
            int t = thickness;
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - t, rect.Width, t), color);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
            sb.Draw(_pixel, new Rectangle(rect.X + rect.Width - t, rect.Y, t, rect.Height), color);
        }
    }
}
