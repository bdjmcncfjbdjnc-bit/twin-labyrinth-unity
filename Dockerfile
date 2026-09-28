FROM node:22-alpine
WORKDIR /app
COPY server.js core.js index.html package.json ./
ENV NODE_ENV=production
USER node
CMD ["node","server.js"]
