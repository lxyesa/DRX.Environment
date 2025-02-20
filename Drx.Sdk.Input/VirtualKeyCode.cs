using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Input
{
    public enum VirtualKeyCode
    {
        // 常用按键
        LBUTTON = 0x01,
        // 鼠标左键
        RBUTTON = 0x02,
        // 鼠标右键
        MBUTTON = 0x04,
        // 鼠标中键
        XBUTTON1 = 0x05,
        // 鼠标侧键1 (后退键)
        XBUTTON2 = 0x06,
        // 鼠标侧键2 (前进键)
        CANCEL = 0x03,
        // Control-break processing
        BACK = 0x08,
        // BACKSPACE键
        TAB = 0x09,
        // TAB键
        RETURN = 0x0D,
        // ENTER键
        SHIFT = 0x10,
        // SHIFT键
        CONTROL = 0x11,
        // CTRL键
        MENU = 0x12,
        // ALT键
        PAUSE = 0x13,
        // PAUSE键
        CAPITAL = 0x14,
        // CAPS LOCK键
        ESCAPE = 0x1B,
        // ESC键
        SPACE = 0x20,
        // SPACEBAR
        PRIOR = 0x21,
        // PAGE UP键
        NEXT = 0x22,
        // PAGE DOWN键
        END = 0x23,
        // END键
        HOME = 0x24,
        // HOME键
        LEFT = 0x25,
        // LEFT ARROW键
        UP = 0x26,
        // UP ARROW键
        RIGHT = 0x27,
        // RIGHT ARROW键
        DOWN = 0x28,
        // DOWN ARROW键

        // 数字键
        KEY_0 = 0x30,
        // 0 键
        KEY_1 = 0x31,
        // 1 键
        KEY_2 = 0x32,
        // 2 键
        KEY_3 = 0x33,
        // 3 键
        KEY_4 = 0x34,
        // 4 键
        KEY_5 = 0x35,
        // 5 键
        KEY_6 = 0x36,
        // 6 键
        KEY_7 = 0x37,
        // 7 键
        KEY_8 = 0x38,
        // 8 键
        KEY_9 = 0x39,
        // 9 键

        // 字母键
        KEY_A = 0x41,
        // A 键
        KEY_B = 0x42,
        // B 键
        KEY_C = 0x43,
        // C 键
        KEY_D = 0x44,
        // D 键
        KEY_E = 0x45,
        // E 键
        KEY_F = 0x46,
        // F 键
        KEY_G = 0x47,
        // G 键
        KEY_H = 0x48,
        // H 键
        KEY_I = 0x49,
        // I 键
        KEY_J = 0x4A,
        // J 键
        KEY_K = 0x4B,
        // K 键
        KEY_L = 0x4C,
        // L 键
        KEY_M = 0x4D,
        // M 键
        KEY_N = 0x4E,
        // N 键
        KEY_O = 0x4F,
        // O 键
        KEY_P = 0x50,
        // P 键
        KEY_Q = 0x51,
        // Q 键
        KEY_R = 0x52,
        // R 键
        KEY_S = 0x53,
        // S 键
        KEY_T = 0x54,
        // T 键
        KEY_U = 0x55,
        // U 键
        KEY_V = 0x56,
        // V 键
        KEY_W = 0x57,
        // W 键
        KEY_X = 0x58,
        // X 键
        KEY_Y = 0x59,
        // Y 键
        KEY_Z = 0x5A,
        // Z 键

        // 功能键
        F1 = 0x70,
        // F1 键
        F2 = 0x71,
        // F2 键
        F3 = 0x72,
        // F3 键
        F4 = 0x73,
        // F4 键
        F5 = 0x74,
        // F5 键
        F6 = 0x75,
        // F6 键
        F7 = 0x76,
        // F7 键
        F8 = 0x77,
        // F8 键
        F9 = 0x78,
        // F9 键
        F10 = 0x79,
        // F10 键
        F11 = 0x7A,
        // F11 键
        F12 = 0x7B,

        // 延迟键
        DELAY_1 = 0x100,
        // 1 毫秒延迟
        DELAY_10 = 0x101,
        // 10 毫秒延迟
        DELAY_100 = 0x102,
        // 100 毫秒延迟
        DELAY_1000 = 0x103,

        // 鼠标事件
        MOUSE_LEFT_CLICK = 0x200,
        // 鼠标左键点击
        MOUSE_RIGHT_CLICK = 0x201,
        // 鼠标右键点击
        MOUSE_MIDDLE_CLICK = 0x202,
        // 鼠标中键点击
        MOUSE_XBUTTON1_CLICK = 0x203,
        // 鼠标侧键1点击
        MOUSE_XBUTTON2_CLICK = 0x204,
        // 鼠标侧键2点击
    }
}
