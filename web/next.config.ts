import type { NextConfig } from "next";

// Panel tamamen istemci tarafında veri çeker (token tarayıcıda); bu yüzden Cache Components /
// partial prefetching kapalı. Docker imajı için standalone çıktı.
const nextConfig: NextConfig = {
  output: "standalone",
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
