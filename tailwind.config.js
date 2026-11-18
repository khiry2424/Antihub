/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Plus Jakarta Sans"', '"Outfit"', 'system-ui', 'sans-serif'],
        display: ['"Outfit"', '"Plus Jakarta Sans"', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', '"Geist Mono"', 'Consolas', 'monospace'],
      },
      colors: {
        hub: {
          bg: '#060911',
          surface: '#0C1220',
          card: '#101829',
          border: '#1E293B',
          accent: '#38BDF8',
        },
      },
      boxShadow: {
        glass: '0 8px 32px 0 rgba(0, 0, 0, 0.45)',
        glow: '0 0 24px -4px rgba(56, 189, 248, 0.25)',
      },
    },
  },
  plugins: [],
};
