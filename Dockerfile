FROM node:22-alpine
WORKDIR /app
COPY package.json core.js server.js index.html style.css game.js renderer.js audio.js runtime-config.js assets.js ./
USER node
ENV NODE_ENV=production
ENV PORT=8787
EXPOSE 8787
HEALTHCHECK --interval=30s --timeout=3s CMD node -e "fetch('http://localhost:'+process.env.PORT+'/healthz').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.js"]
